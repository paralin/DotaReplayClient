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

        public string uid { get; set; }

        public string name { get; set; }

        public string description { get; set; }

        public ulong matchid { get; set; }

        public DateTime createdAt { get; set; }

        public string reviewer { get; set; }

        public bool reviewed { get; set; }

        public DateTime reviewerUntil { get; set; }

        public string reviewerDescription { get; set; }

        public ulong matchtime { get; set; }

        public string hero_to_watch { get; set; }

        public int rating { get; set; }



        public enum Status : uint
        {
           	DOWNLOAD_QUEUE=0,
			DOWNLOADING=1,
			WAITING_FOR_REVIEW=2,
			REVIEWING=3,
			REVIEWED=4,
			REPLAY_UNAVAILABLE=5,
			INVALID_MATCHID=6,
			ACCESS_DENIED=7
        }
    }
}
