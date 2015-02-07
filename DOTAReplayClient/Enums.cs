namespace DOTAReplayClient
{
    public enum MessageIDs : int
    {
        HANDSHAKE = 0
    }

    public enum ServerMessageIDs : int
    {
        HANDSHAKE_RESPONSE = 0,
        UNABLE_TO_VIEW = 1,
        ADD_UPDATE_SUB = 2,
        REMOVE_SUB     = 3,
        USER_INFO      = 4,
        SYSTEM_STATS   = 5
    }
}
