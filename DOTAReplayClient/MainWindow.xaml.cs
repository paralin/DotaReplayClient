using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace DOTAReplayClient
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public class WatchRequest
        {
            public ProgressDialogController progress;
            public string Id;
        }

        public class ReviewRequest
        {
            public string Description;
            public int Rating;
            public string Id { get; set; }
        }

        public bool requestingMore = false;
        public event EventHandler OnClickSignout;
        public event EventHandler<WatchRequest> OnRequestWatch;
        public event EventHandler OnRequestClearReplays;
        public event EventHandler<Action<bool, string>> OnRequestMoreReplays;
        public event EventHandler<ReviewRequest> OnReviewSubmission;
        public ProgressDialogController progress;
        public ObservableCollection<Submission> Submissions = new ObservableCollection<Submission>();
        private string descrip = "";
        private int rating = 1;
        public ObservableCollection<Submission> SubmissionsBind
        {
            get { return Submissions; }
        } 

        public MainWindow()
        {
            InitializeComponent();
            reviewCollectionContainer.Collection = SubmissionsBind;
            //TheTabControl.ItemsSource = SubmissionsBind;
            //TheTabControl.SelectedIndex = 0;
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

        private async void RequestMatches_Click(object sender, RoutedEventArgs e)
        {
            if (OnRequestMoreReplays != null)
            {
                progress = await this.ShowProgressAsync("Requesting more...", "Asking for more to review...");
                progress.SetIndeterminate();
                OnRequestMoreReplays(this, (success, reason) => CrossThread(async () =>
                {
                    await progress.CloseAsync();
                    if (!success)
                        await this.ShowMessageAsync("Unable to request", reason);
                }));
            }
        }

        private void SubmitReview_OnClick(object sender, RoutedEventArgs e)
        {
            if (OnReviewSubmission != null)
            {
                OnReviewSubmission(this, new ReviewRequest() { Description = descrip, Rating = rating, Id = (((Button)e.Source).DataContext as Submission).Id });
                submissionTabs.SelectedIndex = 0;
            }
        }

        private async void WatchReview_OnClick(object sender, RoutedEventArgs e)
        {
            progress = await this.ShowProgressAsync("Please wait...", "Downloading replay from DotaReplay servers...");
            progress.SetProgress(0.05);
            if (OnRequestWatch != null) OnRequestWatch(this, new WatchRequest(){progress = progress, Id = (((Button)e.Source).DataContext as Submission).Id});
        }

        private void Description_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            descrip = ((TextBox) e.Source).Text;
        }

        private int idx = 0;
        private void Tabs_OnSelected(object sender, RoutedEventArgs e)
        {
            if (submissionTabs.SelectedIndex == idx) return;
            descrip = "";
            rating = 1;
            idx = submissionTabs.SelectedIndex;
            try
            {
                
            }
            catch (Exception ex)
            {
                
            }
        }

        private void RatingField_OnSelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if(eventArgs.AddedItems.Count > 0)
                rating = int.Parse((string)((ComboBoxItem) eventArgs.AddedItems[0]).Tag);
        }

        private void ClearReplayDirectory_Click(object sender, RoutedEventArgs e)
        {
            this.ShowMessageAsync("Are you sure?", "This will clear your entire DOTA 2 replay directory.", MessageDialogStyle.AffirmativeAndNegative).ContinueWith(
                (res) =>
                {
                    if (res.Result == MessageDialogResult.Affirmative)
                    {
                        if (OnRequestClearReplays != null) OnRequestClearReplays(this, EventArgs.Empty);
                        CrossThread(async () => await this.ShowMessageAsync("Cleared", "Your replay directory has been cleared."));
                    }
                });
        }
    }
}
