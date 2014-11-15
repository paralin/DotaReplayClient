using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Timers;
using DOTAReplay.Bots;
using DOTAReplay.Data;
using DOTAReplay.Database;
using DOTAReplay.Model;
using DOTAReplay.Storage;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using SteamKit2.GC.Dota.Internal;

namespace DOTAReplay
{
    public class MatchDownloader
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public Timer timer;
        private string tempDir;

        public MatchDownloader()
        {
            timer = new Timer(1000);
            timer.Elapsed += TimerOnElapsed;
            Mongo.Submissions.Update(Query.EQ("status", 1), Update.Set("status", 0));
            tempDir = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "downloads");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            timer.Start();
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            timer.Stop();
            try
            {
                var submissions = Mongo.Submissions.Find(Query.EQ("status", 0)).ToArray();
                if (submissions.Length > 0)
                    Mongo.Submissions.Update(Query.EQ("status", 0), Update.Set("status", 1), UpdateFlags.Multi);
                foreach (var submission in submissions)
                {
                    Submission submission1 = submission;
                    log.Debug("Queuing fetch for " + submission1.matchid);
                    BotDB.FetchReplay(submission.matchid, callback =>
                    {
                        if (callback.StatusCode > DownloadReplayCallback.Status.Success)
                        {
                            log.Debug("Match " + submission1.matchid + " not successful, " + callback.StatusCode);
                            submission1.status =
                                (Submission.Status)
                                    ((uint) Submission.Status.REPLAY_UNAVAILABLE + (callback.StatusCode - 1));
                            Mongo.Submissions.Save(submission1);
                        }
                        else
                        {
                            var result = new MatchResult(callback.Match);
                            Mongo.Results.Save(result);
                            log.Debug("Downloading replay file for " + submission1.matchid);
                            DownloadReplayFile(callback.Match, b =>
                            {
                                log.Debug("Download for " + submission1.matchid + " success: " + b);
                                submission1.status = b
                                    ? Submission.Status.WAITING_FOR_REVIEW
                                    : Submission.Status.REPLAY_UNAVAILABLE;
                                Mongo.Submissions.Save(submission1);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                log.Error("Problem fetching new submissions", ex);
            }
            try
            {
                timer.Start();
            }
            catch (Exception ex)
            {
                log.Error("Issue starting timer in MatchDownloader", ex);
            }
        }

        private void DownloadReplayFile(CMsgDOTAMatch match, Action<bool> success)
        {
            using (WebClient client = new WebClient())
            {
                try
                {
                    var path = Path.Combine(tempDir, match.match_id + ".dem.bz2");
                    client.DownloadFile(
                        string.Format("http://replay{0}.valve.net/570/{1}_{2}.dem.bz2", match.cluster, match.match_id,
                            match.replay_salt), path);
                    success(AmazonS3.UploadFile(path));
                }
                catch (Exception ex)
                {
                    log.Error("Issue downloading " + match.match_id + "...", ex);
                    success(false);
                }
            }
        }
    }
}
