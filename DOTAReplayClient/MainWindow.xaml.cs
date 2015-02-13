using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.Controls;

namespace DOTAReplayClient
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public bool requestingMore = false;
        public event EventHandler OnClickSignout;
        public ObservableCollection<Submission> Submissions = new ObservableCollection<Submission>();

        public ObservableCollection<Submission> SubmissionsBind
        {
            get { return Submissions; }
        } 

        public MainWindow()
        {
            InitializeComponent();
        }

        public void SetUserInfo(UserInfo info)
        {
            steamNameLabel.Text = info.name;
        }

        public void SetStatistics(SystemStats stats)
        {
            allSubsCount.Text = stats.allSubmissions+"";
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            if (OnClickSignout != null) OnClickSignout(this, EventArgs.Empty);
        }

        public void CrossThread(Action action)
        {
            this.Dispatcher.Invoke(action);
        }

        private void RequestMatches_Click(object sender, RoutedEventArgs e)
        {
            if (requestingMore) return;
            requestingMore = true;
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(2000);
                requestingMore = false;
            });
        }
    }
}
