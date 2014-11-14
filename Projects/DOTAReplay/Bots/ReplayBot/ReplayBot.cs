using System;
using System.Collections.Generic;
using System.Threading;
using Appccelerate.StateMachine;
using Appccelerate.StateMachine.Machine;
using DOTAReplay.Bots.ReplayBot.Enums;
using DOTAReplay.Data;
using DOTAReplay.Model;
using KellermanSoftware.CompareNetObjects;
using log4net;
using SteamKit2;
using SteamKit2.GC.Dota.Internal;

namespace DOTAReplay.Bots.ReplayBot
{
    public class ReplayBot
    {
        #region Private Variables
        private ILog log;
        private SteamClient client;
        private SteamUser.LogOnDetails details;
        private DotaGCHandler dota;

        private SteamFriends friends;
        public ActiveStateMachine<States, Events> fsm;

        protected bool isRunning = false;
        private CallbackManager manager;

        public States State = States.Connecting;

        private Bot bot;

        private Thread procThread;
        private bool reconnect;
        private System.Timers.Timer reconnectTimer = new System.Timers.Timer(5000);
        private SteamUser user;
        private uint GCVersion;
        #endregion

        public delegate void LobbyUpdateHandler(CSODOTALobby lobby, ComparisonResult differences);

        public event LobbyUpdateHandler LobbyUpdate;

        private Dictionary<ulong, DownloadReplayCallback> Callbacks = new Dictionary<ulong, DownloadReplayCallback>(); 

        /// <summary>
        /// Setup a new bot with some details.
        /// </summary>
        /// <param name="extensions">any extensions you want on the state machine.</param>
        public ReplayBot(Bot bot, params IExtension<States, Events>[] extensions)
        {
            this.bot = bot;
            this.reconnect = true;
            this.details = new SteamUser.LogOnDetails()
            {
                Username = bot.Username,
                Password = bot.Password
            };
            this.log = LogManager.GetLogger("ReplayBot " + bot.Username);
            log.Debug("Initializing a new ReplayBot, username: " + bot.Username);
            reconnectTimer.Elapsed += (sender, args) =>
            {
                reconnectTimer.Stop();
                fsm.Fire(Events.AttemptReconnect);
            };
            fsm = new ActiveStateMachine<States, Events>();
            foreach (var ext in extensions) fsm.AddExtension(ext);
            fsm.DefineHierarchyOn(States.Connecting)
                .WithHistoryType(HistoryType.None);
            fsm.DefineHierarchyOn(States.Connected)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.Dota);
            fsm.DefineHierarchyOn(States.Dota)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.DotaConnect)
                .WithSubState(States.DotaMenu);
            fsm.DefineHierarchyOn(States.Disconnected)
                .WithHistoryType(HistoryType.None)
                .WithInitialSubState(States.DisconnectNoRetry)
                .WithSubState(States.DisconnectRetry);
            fsm.In(States.Connecting)
                .ExecuteOnEntry(InitAndConnect)
                .On(Events.Connected).Goto(States.Connected)
                .On(Events.Disconnected).Goto(States.DisconnectRetry)
                .On(Events.LogonFailSteamGuard).Goto(States.DisconnectNoRetry) //.Execute(() => reconnect = false)
                .On(Events.LogonFailBadCreds).Goto(States.DisconnectNoRetry);
            fsm.In(States.Connected)
                .ExecuteOnExit(DisconnectAndCleanup)
                .On(Events.Disconnected).If(ShouldReconnect).Goto(States.Connecting)
                .Otherwise().Goto(States.Disconnected);
            fsm.In(States.Disconnected)
                .ExecuteOnEntry(DisconnectAndCleanup)
                .ExecuteOnExit(ClearReconnectTimer)
                .On(Events.AttemptReconnect).Goto(States.Connecting);
            fsm.In(States.DisconnectRetry)
                .ExecuteOnEntry(StartReconnectTimer);
            fsm.In(States.Dota)
                .ExecuteOnExit(DisconnectDota);
            fsm.In(States.DotaConnect)
                .ExecuteOnEntry(ConnectDota)
                .On(Events.DotaGCReady).Goto(States.DotaMenu);
            fsm.In(States.DotaMenu)
                .ExecuteOnEntry(SetOnlinePresence);
            fsm.Initialize(States.Connecting);
        }

        public void Start()
        {
            fsm.Start();
        }

        private void ClearReconnectTimer()
        {
            reconnectTimer.Stop();
        }

        private void DisconnectDota()
        {
            dota.CloseDota();
        }

        public void leaveLobby()
        {
            if (dota.Lobby != null)
            {
                log.Debug("Leaving lobby.");
            }
            dota.AbandonGame();
            dota.LeaveLobby();
        }

        public bool IsFetchingReplay;
        public uint MatchesFetched;
        private ulong MatchId;
        public void FetchMatchResult(DownloadReplayCallback cb)
        {
            MatchId = cb.MatchID;
            Callbacks.Add(MatchId, cb);
            MatchesFetched++;
            IsFetchingReplay = true;
            dota.RequestMatchResult(cb.MatchID);
        }

        private void StartReconnectTimer()
        {
            reconnectTimer.Start();
        }

        private static void SteamThread(object state)
        {
            ReplayBot bot = state as ReplayBot;
            if (bot == null) return;
            while (bot.isRunning && bot.manager != null)
            {
                bot.manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        private bool ShouldReconnect()
        {
            return isRunning && reconnect;
        }

        private void SetOnlinePresence()
        {
            friends.SetPersonaState(EPersonaState.Online);
            friends.SetPersonaName(bot.PersonaName ?? "DOTAReplay Bot");
        }

        private void InitAndConnect()
        {
            if (client == null)
            {
                client = new SteamClient();
                user = client.GetHandler<SteamUser>();
                friends = client.GetHandler<SteamFriends>();
                dota = client.GetHandler<DotaGCHandler>();
                manager = new CallbackManager(client);
                isRunning = true;
                new Callback<SteamClient.ConnectedCallback>(c =>
                {
                    if (c.Result != EResult.OK)
                    {
                        fsm.FirePriority(Events.Disconnected);
                        isRunning = false;
                        return;
                    }

                    user.LogOn(details);
                }, manager);
                new Callback<SteamClient.DisconnectedCallback>(c => fsm.Fire(Events.Disconnected), manager);
                new Callback<SteamUser.LoggedOnCallback>(c =>
                {
                    if (c.Result != EResult.OK)
                    {
                        if (c.Result == EResult.AccountLogonDenied)
                        {
                            fsm.Fire(Events.LogonFailSteamGuard);
                            return;
                        }
                        fsm.Fire(Events.LogonFailBadCreds);
                    }
                    else
                    {
                        fsm.Fire(Events.Connected);
                    }
                }, manager);
                new Callback<DotaGCHandler.MatchResultResponse>(c =>
                {
                    IsFetchingReplay = false;
                    DownloadReplayCallback cb;
                    ulong id;
                    id = c.result.match != null ? c.result.match.match_id : MatchId;
                    if (!Callbacks.TryGetValue(id, out cb)) return;
                    Callbacks.Remove(id);
                    cb.Match = c.result.match;
                    if (c.result.result == (uint) EResult.OK && c.result.match != null &&
                        c.result.match.replay_state != CMsgDOTAMatch.ReplayState.REPLAY_AVAILABLE)
                        c.result.result = 9999; //unavailable
                    switch (c.result.result)
                    {
                        case (uint)EResult.OK:
                            cb.StatusCode = DownloadReplayCallback.Status.Success;
                            break;
                        case (uint)EResult.AccessDenied:
                            cb.StatusCode = DownloadReplayCallback.Status.AccessDenied;
                            break;
                        case (uint)EResult.Invalid:
                            cb.StatusCode = DownloadReplayCallback.Status.InvalidMatch;
                            break;
                        default:
                            cb.StatusCode = DownloadReplayCallback.Status.Unavailable;
                            break;
                    }
                    cb.callback(cb);
                }, manager);
                new Callback<DotaGCHandler.GCWelcomeCallback>(c =>
                {
                    log.Debug("GC welcome, version " + c.Version);
                    this.GCVersion = c.Version;
                    fsm.Fire(Events.DotaGCReady);
                }, manager);
                new Callback<DotaGCHandler.UnhandledDotaGCCallback>(
                    c => log.Debug("Unknown GC message: " + c.Message.MsgType), manager);
                new Callback<SteamFriends.FriendsListCallback>(c => log.Debug(c.FriendList), manager);
                new Callback<DotaGCHandler.PracticeLobbySnapshot>(c =>
                {
                    log.DebugFormat("Lobby snapshot received with state: {0}", c.lobby.state);
                    leaveLobby();
                }, manager);
                new Callback<DotaGCHandler.PingRequest>(c =>
                {
                    log.Debug("GC Sent a ping request. Sending pong!");
                    dota.Pong();
                }, manager);
                new Callback<DotaGCHandler.JoinChatChannelResponse>(
                    c => log.Debug("Joined chat " + c.result.channel_name), manager);
                new Callback<DotaGCHandler.ChatMessage>(
                    c => log.DebugFormat("{0} => {1}: {2}", c.result.channel_id, c.result.persona_name, c.result.text),
                    manager);
                new Callback<DotaGCHandler.OtherJoinedChannel>(
                    c =>
                        log.DebugFormat("User with name {0} joined channel {1}.", c.result.persona_name,
                            c.result.channel_id), manager);
                new Callback<DotaGCHandler.OtherLeftChannel>(
                    c =>
                        log.DebugFormat("User with steamid {0} left channel {1}.", c.result.steam_id,
                            c.result.channel_id), manager);
                new Callback<DotaGCHandler.CacheUnsubscribed>(c => log.Debug("Bot has left/been kicked from the lobby."), manager);
                new Callback<DotaGCHandler.Popup>(c =>
                {
                    log.DebugFormat("Received message (popup) from GC: {0}", c.result.id);
                    if (c.result.id == CMsgDOTAPopup.PopupID.KICKED_FROM_LOBBY)
                    {
                        log.Debug("Kicked from the lobby!");
                    }
                }, manager);
            }
            client.Connect();
            procThread = new Thread(SteamThread);
            procThread.Start(this);
        }

        private void ConnectDota()
        {
            log.Debug("Attempting to connect to Dota...");
            dota.LaunchDota();
        }

        public void DisconnectAndCleanup()
        {
            isRunning = false;
            if (client != null)
            {
                if (user != null)
                {
                    if(dota != null) dota.LeaveLobby();
                    user.LogOff();
                    user = null;
                }
                if (client.IsConnected) client.Disconnect();
                client.ClearHandlers();
                client = null;
            }
        }

        public void Destroy()
        {
            manager.Unregister();
            manager = null;
            if (fsm != null)
            {
                fsm.Stop();
                fsm.ClearExtensions();
                fsm = null;
            }
            reconnect = false;
            DisconnectAndCleanup();
            user = null;
            client = null;
            friends = null;
            dota = null;
            manager = null;
        }


        public void StartGame()
        {
            dota.LaunchLobby();
        }
    }
}
