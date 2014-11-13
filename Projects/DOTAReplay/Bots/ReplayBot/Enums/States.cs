using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOTAReplay.Bots.ReplayBot.Enums
{
    public enum States
    {
        Connecting,
        Disconnected,
        Connected,
        DisconnectNoRetry,
        DisconnectRetry,

        #region DOTA

        Dota,
        DotaConnect,
        DotaMenu

        #endregion
    }
}
