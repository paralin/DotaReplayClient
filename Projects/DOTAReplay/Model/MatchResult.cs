using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2.GC.Dota.Internal;

namespace DOTAReplay.Model
{
    public class MatchResult : CMsgDOTAMatch
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; }
    }
}
