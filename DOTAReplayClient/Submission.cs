using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DOTAReplayClient.Annotations;

namespace DOTAReplayClient
{
    public class Submission : INotifyPropertyChanged
    {
        private string _fetchstatus;
        private bool _fetching;
        private bool _fetchingIndeterminate;
        private int _fetchProgress;
        private bool _ready;
        public string Id { get; set; }

        public Status status { get; set; }

        public string show { get; set; }

        public string uid { get; set; }

        public string uname { get; set; }

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

        public string showname { get; set; }

        public string fetchstatus
        {
            get { return _fetchstatus; }
            set
            {
                if (value == _fetchstatus) return;
                _fetchstatus = value;
                OnPropertyChanged();
            }
        }

        public bool fetching
        {
            get { return _fetching; }
            set
            {
                if (value.Equals(_fetching)) return;
                _fetching = value;
                OnPropertyChanged();
            }
        }

        public bool fetchingIndeterminate
        {
            get { return _fetchingIndeterminate; }
            set
            {
                if (value.Equals(_fetchingIndeterminate)) return;
                _fetchingIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public int fetchProgress
        {
            get { return _fetchProgress; }
            set
            {
                if (value == _fetchProgress) return;
                _fetchProgress = value;
                OnPropertyChanged();
            }
        }

        public bool ready
        {
            get { return _ready; }
            set
            {
                if (value.Equals(_ready)) return;
                _ready = value;
                OnPropertyChanged();
            }
        }


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

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            MainWindow.Instance.CrossThread(() =>
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }
}
