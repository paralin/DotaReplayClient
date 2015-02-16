namespace DOTAReplayClient
{
    public enum MessageIDs : int
    {
        HANDSHAKE = 0,
        REQUEST_DOWNLOAD = 1,
        REQUEST_REVIEW   = 2,
        SUBMIT_REVIEW    = 3
    }

    public enum ServerMessageIDs : int
    {
        HANDSHAKE_RESPONSE = 0,
        UNABLE_TO_VIEW = 1,
        ADD_UPDATE_SUB = 2,
        REMOVE_SUB     = 3,
        USER_INFO      = 4,
        SYSTEM_STATS   = 5,
        REQUEST_REPLYR = 6,
        REQREV_REPLYR  = 7,

        INVALID_VERSION = 9999
    }
}
