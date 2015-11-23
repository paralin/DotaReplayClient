namespace DOTAReplayClient
{
    public class UserInfo
    {
        public string name { get; set; }
        public string[] roles { get; set; }
        public SteamService steam { get; set; }
    }

    public class SteamService
    {
        public int communityvisibilitystate { get; set; }
        public int profilestate { get; set; }
        public string personaname { get; set; }
        public double lastlogoff { get; set; }
        public int commentpermission { get; set; }
        public string profileurl { get; set; }
        public AvatarInfo avatar { get; set; }
        public int personastate { get; set; }
        public string realname { get; set; }
        public string primaryclanid { get; set; }
        public double timecreated { get; set; }
        public int personastateflags { get; set; }
        public string loccountrycode { get; set; }
        public string locstatecode { get; set; }
        public int loccityid { get; set; }
        public string id { get; set; }
        public string username { get; set; }
    }

    public class AvatarInfo
    {
        public string small { get; set; }
        public string medium { get; set; }
        public string full { get; set; }
    }
}