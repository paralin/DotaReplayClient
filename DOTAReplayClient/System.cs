using System.Collections.Generic;
using System.Windows;
using DOTAReplayClient.Properties;

namespace DOTAReplayClient
{
    public class DRClientManager
    {
        public static DRClientManager Instance;
#if USE_ICON
        private TrayIcon icon;
#endif
        private LoadingWindow window;
        private DRClient client;
        private MainWindow mainWindow = null;
        Dictionary<string, Submission> Submissions = new Dictionary<string, Submission>();
        private bool reconnecting = false;

        public DRClientManager()
        {
            Instance = this;
            //icon = new TrayIcon();
            Application.Current.Exit += (sender, args) => Shutdown(false);
            window = new LoadingWindow();
            window.OnTokenInputted += (sender, s) =>
            {
                Settings.Default.Token = s;
                Settings.Default.Save();
                client.SendHandshake();
            };
            window.Closed += (o, args) => Shutdown();
            client = new DRClient();
            client.HandshakeResponse += (sender, b) =>
            {
                reconnecting = false;
                if (!b)
                {
                    window.SetTokenInputMode(true);
                }
                else
                {
                    window.CrossThread(() =>
                    {
                        window.Hide();
                        mainWindow.Show();
                    });
                }
            };
            client.NotAReviewer += delegate {
                window.CrossThread(async () =>
                {
                    if (mainWindow != null && mainWindow.IsVisible) mainWindow.Hide();
                    if (!window.IsVisible) window.Show();
                    await window.ShowCannotUse();
                    Shutdown();
                });
            };
            client.OnUserInfo += (sender, info) => window.CrossThread(() => mainWindow.SetUserInfo(info));
            client.OnSystemStats += (sender, info) => window.CrossThread(() => mainWindow.SetStatistics(info));
            client.AddUpdateSub += (sender, submission) =>
            {
                if (!Submissions.ContainsKey(submission.Id)) window.CrossThread(() => mainWindow.Submissions.Add(submission));
                Submissions[submission.Id] = submission;
            };
            client.RemoveSub += (sender, s) =>
            {
                if (Submissions.ContainsKey(s)) mainWindow.Submissions.Remove(Submissions[s]);
                Submissions.Remove(s);
            };
            client.ConnectionClosed += (sender, args) => window.CrossThread(async () =>
            {
                if (reconnecting) return;
                if (mainWindow != null && mainWindow.IsVisible) mainWindow.Hide();
                if(!window.IsVisible) window.Show();
                await window.ShowCannotConnect();
                Shutdown();
            });
            mainWindow = null;
        }

        public void Shutdown(bool doexit = true)
        {
#if USE_ICON
            if (icon != null)
            {
                icon.Dispose();
                icon = null;
            }
#endif
            if(doexit)
                Application.Current.Shutdown();
        }

        public void Signout()
        {
            Settings.Default.Token = "";
            Settings.Default.Save();
            reconnecting = true;
            client.Stop();
            Start();
        }

        public void Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            window.SetTokenInputMode(false);
            window.Show();
            if (mainWindow != null)
            {
                mainWindow.Hide();
            }else{
                mainWindow = new MainWindow();
                mainWindow.Closed += (sender, args) => Shutdown();
                mainWindow.OnClickSignout += (sender, args) => Signout();
            }
            client.Start();
        }
    }
}
