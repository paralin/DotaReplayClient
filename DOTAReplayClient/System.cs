using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using DOTAReplayClient.Properties;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Core;
using MahApps.Metro.Controls.Dialogs;


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
        private string dotaPath;
        private string steamPath;

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
            client.OnInvalidVersion += (sender, args) =>
            {
                reconnecting = false;
                window.SetTokenInputMode(false);
                window.CrossThread(async () =>
                {
                    await window.ShowOutdatedVersion();
                    Shutdown(true);
                });
            };
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
                if (!Submissions.ContainsKey(submission.Id)) mainWindow.CrossThread(() => mainWindow.Submissions.Add(submission));
                Submissions[submission.Id] = submission;
            };
            client.RemoveSub += (sender, s) =>
            {
                if (Submissions.ContainsKey(s)) mainWindow.CrossThread(() => mainWindow.Submissions.Remove(Submissions[s]));
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

        public async void Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            {
                var sf = new SteamFinder();
                dotaPath = sf.FindDota(true, false);
                steamPath = sf.FindSteam(false, false);
            }
            window.SetTokenInputMode(false);
            window.Show();
            if (dotaPath == null)
            {
                await window.ShowCannotFindSteam();
                Shutdown(true);
                return;
            }
            if (mainWindow != null)
            {
                mainWindow.Hide();
            }else{
                mainWindow = new MainWindow();
                mainWindow.Closed += (sender, args) => Shutdown();
                mainWindow.OnClickSignout += (sender, args) => Signout();
                mainWindow.OnRequestWatch += (sender, controller) => DownloadWithProgress(controller);
                mainWindow.OnRequestClearReplays += (sender, args) =>
                {
                    var replayDir = new DirectoryInfo(dotaPath + "/dota/replays/");
                    foreach (FileInfo file in replayDir.GetFiles())
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch 
                        {
                        }
                    }
                };
                mainWindow.OnRequestMoreReplays += (sender, cb) => client.SendRequestMore(cb);
                mainWindow.OnReviewSubmission += (sender, data) => client.SendReview(data);
            }
            client.Start();
        }

        private void LaunchDOTA(double matchid, double time)
        {
            Process.Start(Path.Combine(dotaPath, "dota.exe"), "dota2://matchid=" + matchid + "&matchtime=" + time);
            //Clipboard.SetText("bind \"p\" \"demo_goto "+(matchtime*30)+"\"; playdemo replays/" + matchid);
            //await mainWindow.ShowMessageAsync("Play replay", "Paste copied command into the DOTA console, then press P.");
        }

        private void DownloadWithProgress(MainWindow.WatchRequest controller)
        {
            controller.progress.SetProgress(0.1);
            client.RequestDownloadURL(controller.Id, (success, data, matchid, matchtime) =>
            {
                if (!success)
                {
                    mainWindow.CrossThread(async () =>
                    {
                        await mainWindow.progress.CloseAsync();
                        controller.progress = null;
                        await mainWindow.ShowMessageAsync("Problem downloading", data, MessageDialogStyle.Affirmative, new MetroDialogSettings(){AffirmativeButtonText = "Close"});
                    });
                    return;
                }
                controller.progress.SetProgress(0.2);
                string downloadPath = dotaPath + "/dota/replays/" + matchid + ".dem.bz2";
                if (File.Exists(dotaPath + "/dota/replays/" + matchid + ".dem"))
                {
                    mainWindow.CrossThread(async () =>
                    {
                        await controller.progress.CloseAsync();
                        controller.progress = null;
                        LaunchDOTA(matchid, matchtime);
                    });
                    return;
                }
                using (WebClient dlcl = new WebClient())
                {
                    dlcl.DownloadProgressChanged +=
                        (sender, args) => controller.progress.SetProgress(args.ProgressPercentage/100.0);
                    dlcl.DownloadFileCompleted += async (sender, args) => {
                        mainWindow.CrossThread(() => mainWindow.progress.SetIndeterminate());
                        var dataBuffer = new byte[4096];

                        using (System.IO.Stream fs = new FileStream(downloadPath, FileMode.Open, FileAccess.Read))
                        {
                            using (var gzipStream = new BZip2InputStream(fs))
                            {
                                string fnOut = Path.Combine(dotaPath + "/dota/replays/", ""+matchid+".dem");

                                using (FileStream fsOut = File.Create(fnOut))
                                {
                                    StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                                }
                            }
                        }
                        File.Delete(downloadPath);
                        mainWindow.CrossThread(async () =>
                        {
                            await controller.progress.CloseAsync();
                            controller.progress = null;
                            LaunchDOTA(matchid, matchtime);
                        });
                    };
                    dlcl.DownloadFileAsync(new Uri(data), downloadPath);
                }
            });
        }
    }
}
