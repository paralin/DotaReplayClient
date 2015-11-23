using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace DOTAReplayClient
{
    public static class Utils
    {
        public static void Send(this WebSocket sock, object obj)
        {
            sock.Send(JObject.FromObject(obj).ToString(Formatting.None));
        }
    }
}