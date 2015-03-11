using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
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
        private HashSet<string> pendingDownloads = new HashSet<string>(); 

        LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(2);
        List<Task> tasks = new List<Task>();
        private TaskFactory factory;
        CancellationTokenSource cts = new CancellationTokenSource();

        public DRClientManager()
        {
            factory = new TaskFactory(lcts);
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
                if (!Submissions.ContainsKey(submission.Id))
                {
                    mainWindow.CrossThread(() => mainWindow.Submissions.Add(submission));
                    AsyncFetch(submission);
                }
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

        private void AsyncFetch(Submission submission)
        {
            submission.fetching = true;
            submission.fetchingIndeterminate = true;
            submission.fetchstatus = "Waiting to download...";
            factory.StartNew(() => DownloadWithProgress(submission), cts.Token);
        }

        private bool exited = false;

        public void Shutdown(bool doexit = true)
        {
#if USE_ICON
            if (icon != null)
            {
                icon.Dispose();
                icon = null;
            }
#endif
            if(doexit && !exited)
            {
                cts.Cancel();
                exited = true;
                if (pendingDownloads.Count > 0)
                {
                    ProcessStartInfo Info = new ProcessStartInfo();
                    Info.Arguments = "/C choice /C Y /N /D Y /T 1 & Del";
                    foreach (var down in pendingDownloads)
                    {
                        Info.Arguments += " \"" + down.Replace('/', '\\') + "\"";
                    }
                    Console.WriteLine(Info.Arguments);
                    Info.WindowStyle = ProcessWindowStyle.Hidden;
                    Info.CreateNoWindow = true;
                    Info.FileName = "cmd.exe";
                    Process.Start(Info); 
                }
                Application.Current.Shutdown();
            }
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
                mainWindow.OnRequestWatch += (sender, controller) => LaunchReplay(controller);
                mainWindow.OnRequestWatchManual += (sender, controller) => DownloadWatchWithOverlay(controller);
                mainWindow.OnRequestClearReplays += (sender, args) =>
                {
                    var replayDir = new DirectoryInfo(dotaPath + "/dota/replays/");
                    foreach (FileInfo file in replayDir.GetFiles())
                    {
                        try
                        {
                            var id = file.Name.Split('.')[0];
                            if (!Submissions.ContainsKey(id))
                            {
                                file.Delete();
                            }
                        }
                        catch 
                        {
                        }
                    }
                };
                mainWindow.OnClickRetryDownloads += (sender, args) =>
                {
                    foreach (var sub in Submissions.Values)
                    {
                        if(!sub.fetching) AsyncFetch(sub);
                    }
                };
                mainWindow.OnRequestMoreReplays += (sender, cb) => client.SendRequestMore(cb);
                mainWindow.OnReviewSubmission += (sender, data) => client.SendReview(data);
            }
            client.Start();
        }

        private void DownloadWatchWithOverlay(MainWindow.WatchRequest controller)
        {
            Task.Factory.StartNew(() =>
            {
                mainWindow.CrossThread(() =>
                {
                    controller.progress.SetIndeterminate();
                    controller.progress.SetMessage("Checking to see if we have it stored...");
                });
                
                client.RequestDownloadURL(controller.Id, async (success, data, matchid, matchtime) =>
                {
                    string downloadPath = dotaPath + "/dota/replays/" + controller.Id + ".dem.bz2";
                    string targetPath = Path.Combine(dotaPath + "/dota/replays/", controller.Id + ".dem");
                    if (File.Exists(targetPath))
                    {
                        mainWindow.CrossThread(async () => await controller.progress.CloseAsync());
                        LaunchDOTA(double.Parse(controller.Id), matchtime);
                        return;
                    }
                    if (!success)
                    {
                        mainWindow.CrossThread(async () =>
                        {
                            await controller.progress.CloseAsync();
                            await mainWindow.ShowMessageAsync("Problem downloading " + controller.Id, data, MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "Close" });
                        });
                        return;
                    }
                    mainWindow.CrossThread(() => controller.progress.SetProgress(0.2));
                    pendingDownloads.Add(downloadPath);
                    try
                    {
                        using (WebClient dlcl = new WebClient())
                        {
                            dlcl.DownloadProgressChanged +=
                                (sender, args) => mainWindow.CrossThread(() => controller.progress.SetProgress(args.ProgressPercentage/100.0));
                            dlcl.DownloadFileCompleted += async (sender, args) =>
                            {
                                try
                                {
                                    pendingDownloads.Add(targetPath);
                                    mainWindow.CrossThread(()=>
                                    {
                                        controller.progress.SetMessage("Extracting replay...");
                                        controller.progress.SetIndeterminate();
                                    });
                                    var dataBuffer = new byte[4096];

                                    using (
                                        System.IO.Stream fs = new FileStream(downloadPath, FileMode.Open, FileAccess.Read))
                                    {
                                        using (var gzipStream = new BZip2InputStream(fs))
                                        {
                                            string fnOut = targetPath;

                                            using (FileStream fsOut = File.Create(fnOut))
                                            {
                                                StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                                            }
                                        }
                                    }
                                    File.Delete(downloadPath);
                                    pendingDownloads.Remove(downloadPath);
                                    mainWindow.CrossThread(async () =>
                                    {
                                        await controller.progress.CloseAsync();
                                    });
                                    pendingDownloads.Remove(targetPath);
                                    LaunchDOTA(double.Parse(controller.Id), matchtime);
                                }
                                catch (Exception ex)
                                {
                                    mainWindow.CrossThread(async () =>
                                    {
                                        await controller.progress.CloseAsync();
                                        await mainWindow.ShowMessageAsync("Problem downloading " + controller.Id, data, MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "Close" });
                                    });
                                }
                            };
                            dlcl.DownloadFileAsync(new Uri(data), downloadPath);
                            mainWindow.CrossThread(()=>controller.progress.SetMessage("Downloading replay file..."));
                        }
                    }
                    catch (Exception ex)
                    {
                        mainWindow.CrossThread(async () =>
                        {
                            await controller.progress.CloseAsync();
                            await mainWindow.ShowMessageAsync("Problem downloading " + controller.Id, data, MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "Close" });
                        });
                    }
                }, true);
            }, cts.Token);
            
        }

        private void LaunchReplay(MainWindow.WatchRequest controller)
        {
            if (!Submissions.ContainsKey(controller.Id)) return;
            var sub = Submissions[controller.Id];
            if (!sub.ready) return;
            LaunchDOTA(sub.matchid, sub.matchtime);
        }

        private void LaunchDOTA(double matchid, double time)
        {
            Process.Start(Path.Combine(dotaPath, "dota.exe"), "dota2://matchid=" + matchid + "&matchtime=" + time);
        }

        private void DownloadWithProgress(Submission sub)
        {
            sub.fetchstatus = "Downloading replay from DotaReplay servers...";
            sub.fetchingIndeterminate = false;
            sub.fetching = true;
            sub.fetchProgress = 10;
            client.RequestDownloadURL(sub.Id, (success, data, matchid, matchtime) =>
            {
                if (!success)
                {
                    mainWindow.CrossThread(async () =>
                    {
                        sub.fetchProgress = 0;
                        sub.fetching = false;
                        sub.ready = false;
                        sub.fetchingIndeterminate = false;
                        await mainWindow.ShowMessageAsync("Problem downloading "+sub.matchid, data, MessageDialogStyle.Affirmative, new MetroDialogSettings(){AffirmativeButtonText = "Close"});
                    });
                    return;
                }
                sub.fetchProgress = 20;
                string downloadPath = dotaPath + "/dota/replays/" + matchid + ".dem.bz2";
                string targetPath = Path.Combine(dotaPath + "/dota/replays/", ""+matchid+".dem");
                if (File.Exists(targetPath))
                {
                    sub.fetchProgress = 0;
                    sub.fetchingIndeterminate = false;
                    sub.fetching = false;
                    sub.ready = true;
                    return;
                }
                pendingDownloads.Add(downloadPath);
                try
                {
                    using (WebClient dlcl = new WebClient())
                    {
                        dlcl.DownloadProgressChanged +=
                            (sender, args) => sub.fetchProgress = args.ProgressPercentage;
                        dlcl.DownloadFileCompleted += async (sender, args) =>
                        {
                            try
                            {
                                pendingDownloads.Add(targetPath);
                                sub.fetchstatus = "Extracting replay...";
                                sub.fetchingIndeterminate = true;
                                var dataBuffer = new byte[4096];

                                using (
                                    System.IO.Stream fs = new FileStream(downloadPath, FileMode.Open, FileAccess.Read))
                                {
                                    using (var gzipStream = new BZip2InputStream(fs))
                                    {
                                        string fnOut = targetPath;

                                        using (FileStream fsOut = File.Create(fnOut))
                                        {
                                            StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                                        }
                                    }
                                }
                                File.Delete(downloadPath);
                                pendingDownloads.Remove(downloadPath);
                                sub.fetching = sub.fetchingIndeterminate = false;
                                sub.ready = true;
                                pendingDownloads.Remove(targetPath);
                            }
                            catch (Exception ex)
                            {
                                mainWindow.CrossThread(async () =>
                                {
                                    sub.fetchProgress = 0;
                                    sub.fetching = false;
                                    sub.ready = false;
                                    sub.fetchingIndeterminate = false;
                                    await mainWindow.ShowMessageAsync("Problem downloading " + sub.matchid, data, MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "Close" });
                                });
                            }
                        };
                        dlcl.DownloadFileAsync(new Uri(data), downloadPath);
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.CrossThread(async () =>
                    {
                        sub.fetchProgress = 0;
                        sub.fetching = false;
                        sub.ready = false;
                        sub.fetchingIndeterminate = false;
                        await mainWindow.ShowMessageAsync("Problem downloading " + sub.matchid, data, MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "Close" });
                    });
                }
            });
        }
    }
}
