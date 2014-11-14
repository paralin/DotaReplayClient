using System;
using SteamKit2.GC.Dota.Internal;

namespace DOTAReplay.Data
{
    public class DownloadReplayCallback
    {
        public ulong MatchID { get; set; }

        public Status StatusCode { get; set; }

        public Action<DownloadReplayCallback> callback { get; set; }

        public CMsgDOTAMatch Match { get; set; }

        public enum Status : uint
        {
            Success = 0,
            Unavailable,
            InvalidMatch,
            AccessDenied
        }
    }
}
