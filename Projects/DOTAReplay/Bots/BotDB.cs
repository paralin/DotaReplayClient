using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Timers;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using DOTAReplay.Bots.ReplayBot.Enums;
using DOTAReplay.Database;
using DOTAReplay.Model;
using DOTAReplay.Properties;
using log4net;
using MongoDB.Driver.Builders;

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

        /// <summary>
        /// Bot Dictionary
        /// </summary>
        public static ConcurrentDictionary<string, Bot> Bots = new ConcurrentDictionary<string, Bot>();

        public static ConcurrentDictionary<string, ReplayBot.ReplayBot> ActiveBots =
            new ConcurrentDictionary<string, ReplayBot.ReplayBot>();

        static BotDB()
        {
            UpdateTimer = new Timer(15000);
            UpdateTimer.Elapsed += UpdateTimerOnElapsed;
            UpdateDB();
            UpdateTimer.Start();
        }

        private static void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            UpdateDB();
        }

        private static void CheckActiveBots()
        {
            var availableBots =
                Bots.Where(m => !m.Value.Invalid && !ActiveBots.ContainsKey(m.Key))
                    .Take(Settings.Default.BotCount - ActiveBots.Count);
            foreach (var bot in availableBots)
            {
                log.Debug("Starting new bot " + bot.Key);
                var abot = ActiveBots[bot.Key] = new ReplayBot.ReplayBot(bot.Value,new BotExtension(bot.Value));
                abot.Start();
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
                CheckActiveBots();
            }
            catch (Exception ex)
            {
                log.Error("Mongo connection failure? ", ex);
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
            if (newState.Id == States.DisconnectNoRetry)
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