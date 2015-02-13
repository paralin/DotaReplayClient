using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
using MahApps.Metro.Controls.Dialogs;

namespace DOTAReplayClient
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoadingWindow : MetroWindow
    {
        public LoadingWindow()
        {
            InitializeComponent();
        }

        public event EventHandler<string> OnTokenInputted;

        public void SetTokenInputMode(bool mode)
        {
            CrossThread(() =>
            {
                tokenInputBox.Visibility = mode ? Visibility.Visible : Visibility.Hidden;
                loadingProgressBar.Visibility = mode ? Visibility.Hidden : Visibility.Visible;
            });
        }

        public async Task ShowCannotConnect()
        {
            await this.ShowMessageAsync("Cannot Connect", "Unable to connect to the server.", MessageDialogStyle.Affirmative, new MetroDialogSettings()
            {
                AffirmativeButtonText = "Quit",
                ColorScheme = MetroDialogColorScheme.Inverted,
                AnimateHide = false
            });
        }

        public async Task ShowCannotUse()
        {
            await this.ShowMessageAsync("No Permissions", "You do not have permission to review replays.", MessageDialogStyle.Affirmative, new MetroDialogSettings()
            {
                AffirmativeButtonText = "Quit",
                ColorScheme = MetroDialogColorScheme.Inverted,
                AnimateHide = false
            });
        }

        public void CrossThread(Action action)
        {
            this.Dispatcher.Invoke(action);
        }

        private void TokenKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SetTokenInputMode(false);
                if (OnTokenInputted != null) OnTokenInputted(this, tokenInputBox.Text);
                tokenInputBox.Text = "";
            }
        }
    }
}
