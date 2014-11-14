using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOTAReplay.Model
{
    public class Submission
    {
        public string Id { get; set; }

        public Status status { get; set; }

        public string show { get; set; }

        public uint episode { get; set; }

        public string uid { get; set; }

        public string name { get; set; }

        public string description { get; set; }

        public ulong matchid { get; set; }

        public DateTime createdAt { get; set; }

        public enum Status : uint
        {
           	DOWNLOAD_QUEUE=0,
			DOWNLOADING=1,
			WAITING_FOR_REVIEW=2,
			ACCEPTED=3,
			DECLINED=4,
			REPLAY_UNAVAILABLE=5,
			INVALID_MATCHID=6,
			ACCESS_DENIED=7
        }
    }
}
