namespace DOTAReplay.Data
{
    public class DownloadReplayCallback
    {
        public uint MatchID { get; set; }

        public Status StatusCode { get; set; }

        public enum Status : uint
        {
            Success = 0,
            Unavailable,
            InvalidMatch,
            AccessDenied
        }
    }
}
