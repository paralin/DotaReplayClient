using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using DOTAReplay.Bots.ReplayBot.Enums;
using DOTAReplay.Data;
using DOTAReplay.Database;
using DOTAReplay.Model;
using DOTAReplay.Properties;
using log4net;
using MongoDB.Driver.Builders;
using SteamKit2;

namespace DOTAReplay.Bots
{
    /// <summary>
    /// Keeps track of all of the bots.
    /// </summary>
    public static class BotDB
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static Timer UpdateTimer;
        public static Timer DownloadTimer;

        public static Queue<DownloadReplayCallback> FetchQueue = new Queue<DownloadReplayCallback>();

        /// <summary>
        /// Bot Dictionary
        /// </summary>
        public static ConcurrentDictionary<string, Bot> Bots = new ConcurrentDictionary<string, Bot>();

        public static ConcurrentDictionary<string, ReplayBot.ReplayBot> ActiveBots =
            new ConcurrentDictionary<string, ReplayBot.ReplayBot>();

        public static int TargetBotCount;

        static BotDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;
            DownloadTimer = new Timer(3000);
            DownloadTimer.Elapsed += (sender, args) => CheckActiveBots();
            UpdateDB();
            UpdateTimer.Start();
            DownloadTimer.Start();
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateDB();
        }

        public static void FetchReplay(ulong matchId, Action<DownloadReplayCallback> callback)
        {
            lock (FetchQueue)
            {
                FetchQueue.Enqueue(new DownloadReplayCallback
                {
                    callback = cb =>
                    {
                        CheckActiveBots();
                        callback(cb);
                    },
                    MatchID = matchId
                });
            }
        }

        private static void CheckActiveBots()
        {
            //Calculate target bot count 
            TargetBotCount = FetchQueue.Count;
            if (TargetBotCount > Settings.Default.BotCount) TargetBotCount = Settings.Default.BotCount;
            var numToStartup = TargetBotCount - ActiveBots.Count;
            if (numToStartup < 0) numToStartup = 0;
            var numToShutdown = ActiveBots.Count - TargetBotCount;
            if (numToShutdown < 0) numToShutdown = 0;
            if (numToStartup + numToShutdown != 0)
            {
                log.Debug("Target bot count: " + TargetBotCount);
                log.Debug("Active bot count: " + ActiveBots.Count);
                var toShutdown = ActiveBots
                    .Where(m => !m.Value.IsFetchingReplay)
                    .OrderBy(m => (int)m.Value.State)
                    .Take(numToShutdown)
                    .Concat(ActiveBots.Where(m => m.Value.MatchesFetched > Settings.Default.MaxFetchPerSession && !m.Value.IsFetchingReplay));
                foreach (var bot in toShutdown)
                {
                    log.Debug("Shutting down bot " + bot.Key);
                    ReplayBot.ReplayBot botb;
                    ActiveBots.TryRemove(bot.Key, out botb);
                    bot.Value.Destroy();
                }
                var availableBots =
                    Bots.Where(m => !m.Value.Invalid && !ActiveBots.ContainsKey(m.Key))
                        .OrderBy(m => m.Value.MatchesDownloaded)
                        .Take(numToStartup);
                foreach (var bot in availableBots)
                {
                    log.Debug("Starting new bot " + bot.Key);
                    var abot = ActiveBots[bot.Key] = new ReplayBot.ReplayBot(bot.Value, new BotExtension(bot.Value));
                    abot.Start();
                }
            }
            CheckDownloadQueue();
        }

        public static void CheckDownloadQueue()
        {
            lock (FetchQueue)
            {
                if (FetchQueue.Count == 0) return;
            }
            var bots =
                    ActiveBots.Where(m => !m.Value.IsFetchingReplay && m.Value.State == States.DotaMenu)
                        .OrderBy(m => Bots[m.Key].MatchesDownloaded);
            foreach (var bot in bots.TakeWhile(bot => FetchQueue.Count != 0))
            {
                DownloadReplayCallback cb;
                lock (FetchQueue)
                {
                    cb = FetchQueue.Dequeue();
                }
                bot.Value.FetchMatchResult(cb);
                Bots[bot.Key].MatchesDownloaded++;
                Mongo.Bots.Save(Bots[bot.Key]);
            }
        }

        /// <summary>
        /// Check for differences in the DB
        /// </summary>
        internal static void UpdateDB()
        {
            var bots = Mongo.Bots.FindAs<Bot>(Query.Or(Query.NotExists("Invalid"), Query.EQ("Invalid", false)));
            try
            {
                foreach (var bot in bots)
                {
                    Bot exist = null;
                    if (!Bots.TryGetValue(bot.Id, out exist))
                    {
                        log.Debug("BOT ADDED [" + bot.Id + "]" + " [" + bot.Username + "]");
                        Bots[bot.Id] = bot;
                    }
                    else if (exist.Username != bot.Username || exist.Password != bot.Password)
                    {
                        log.Debug("BOT UPDATE USERNAME [" + exist.Username + "] => [" + bot.Username + "] PASSWORD [" +
                                  exist.Password + "] => [" + bot.Password + "]");
                        Bots[bot.Id] = bot;
                    }
                }
                foreach (var bot in Bots.Values.Where(bot => bots.All(m => m.Id != bot.Id)))
                {
                    Bot outBot;
                    Bots.TryRemove(bot.Id, out outBot);
                    log.Debug("BOT REMOVED/INVALID [" + bot.Id + "] [" + bot.Username + "]");
                }
            }
            catch (Exception ex)
            {
                log.Error("Mongo connection failure? ", ex);
            }
            try
            {
                CheckActiveBots();
            }
            catch (Exception ex)
            {
                log.Error("Problem checking active bots", ex);
            }
        }

        public static void Shutdown()
        {
            foreach (var bot in ActiveBots)
            {
                bot.Value.Destroy();
            }
            ActiveBots.Clear();
        }
    }

    public class BotExtension : IExtension<States, Events>
    {
        private ILog log;
        private Bot bot;

        public BotExtension(Bot bot)
        {
            this.bot = bot;
            log = LogManager.GetLogger("LobbyBotE " + bot.Username);
        }

        public void StartedStateMachine(IStateMachineInformation<States, Events> stateMachine)
        {
        }

        public void StoppedStateMachine(IStateMachineInformation<States, Events> stateMachine)
        {
        }

        public void EventQueued(IStateMachineInformation<States, Events> stateMachine, Events eventId, object eventArgument)
        {
        }

        public void EventQueuedWithPriority(IStateMachineInformation<States, Events> stateMachine, Events eventId, object eventArgument)
        {
        }

        public void SwitchedState(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> oldState, IState<States, Events> newState)
        {
            log.Debug("Switched state to " + newState.Id);
            if (newState.Id == States.DisconnectNoRetry && BotDB.ActiveBots.ContainsKey(bot.Id))
            {
                bot.Invalid = true;
                Mongo.Bots.Save(bot);
                ReplayBot.ReplayBot outbot;
                BotDB.ActiveBots.TryRemove(bot.Id, out outbot);
                if (outbot != null)
                {
                    outbot.Destroy();
                }
                BotDB.UpdateDB();
            }
            else
            {
                ReplayBot.ReplayBot outbot = BotDB.ActiveBots[bot.Id];
                outbot.State = newState.Id;
                if (newState.Id == States.DotaMenu) BotDB.CheckDownloadQueue();
            }
        }

        public void InitializingStateMachine(IStateMachineInformation<States, Events> stateMachine, ref States initialState)
        {
        }

        public void InitializedStateMachine(IStateMachineInformation<States, Events> stateMachine, States initialState)
        {
        }

        public void EnteringInitialState(IStateMachineInformation<States, Events> stateMachine, States state)
        {
        }

        public void EnteredInitialState(IStateMachineInformation<States, Events> stateMachine, States state, ITransitionContext<States, Events> context)
        {
        }

        public void FiringEvent(IStateMachineInformation<States, Events> stateMachine, ref Events eventId, ref object eventArgument)
        {
        }

        public void FiredEvent(IStateMachineInformation<States, Events> stateMachine, ITransitionContext<States, Events> context)
        {
        }

        public void HandlingEntryActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            ref Exception exception)
        {
        }

        public void HandledEntryActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            Exception exception)
        {
        }

        public void HandlingExitActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            ref Exception exception)
        {
        }

        public void HandledExitActionException(IStateMachineInformation<States, Events> stateMachine, IState<States, Events> state, ITransitionContext<States, Events> context,
            Exception exception)
        {
        }

        public void HandlingGuardException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, ref Exception exception)
        {
        }

        public void HandledGuardException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, Exception exception)
        {
        }

        public void HandlingTransitionException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context, ref Exception exception)
        {
        }

        public void HandledTransitionException(IStateMachineInformation<States, Events> stateMachine, ITransition<States, Events> transition,
            ITransitionContext<States, Events> transitionContext, Exception exception)
        {
        }

        public void SkippedTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
        }

        public void ExecutingTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
        }

        public void ExecutedTransition(IStateMachineInformation<States, Events> stateMachineInformation, ITransition<States, Events> transition,
            ITransitionContext<States, Events> context)
        {
        }
    }
}