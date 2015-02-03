using System;
using System.Globalization;
using DOTAReplayClient.Properties;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace DOTAReplayClient
{
    public class DRClient
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private WebSocket socket;
         
        public DRClient()
        {
#if DEBUG
            socket = new WebSocket("ws://192.168.1.46:10304");
#else
            socket = new WebSocket("ws://replay.paral.in:10304");
#endif
            socket.OnClose += OnSocketClose;
            socket.OnOpen += OnSocketOpen;
            socket.OnMessage += ProcessMessage;
            socket.OnError += HandleError;
            log.Debug("Setting up socket...");
        }

        public void Start()
        {
            log.Debug("Connecting to server...");
            socket.ConnectAsync();
        }

        private void HandleError(object sender, ErrorEventArgs errorEventArgs)
        {
            log.Warn("Websocket error: "+errorEventArgs.Message);
        }

        private void ProcessMessage(object sender, MessageEventArgs messageEventArgs)
        {
            if (messageEventArgs.Data.Length == 0)
            {
                log.Debug("Ignoring empty data....");
                return;
            }
            log.Debug("Message: "+messageEventArgs.Data);
            JObject obj = JObject.Parse(messageEventArgs.Data);
            switch ((ServerMessageIDs)obj["m"].Value<int>())
            {
                case ServerMessageIDs.HANDSHAKE_RESPONSE:
                    log.Debug("Handshake success: "+obj["success"].Value<bool>());
                    break;
            }
        }

        private void OnSocketOpen(object sender, EventArgs eventArgs)
        {
            log.Info("Socket with server opened.");
            SendHandshake();
        }

        private void OnSocketClose(object sender, CloseEventArgs closeEventArgs)
        {
            log.Info("Socket with server closed!");
        }

        private void SendHandshake()
        {
            log.Debug("Sending handshake....");
            socket.Send(new
            {
                m = MessageIDs.HANDSHAKE,
                token = Settings.Default.Token
            });
        }
    }
}
