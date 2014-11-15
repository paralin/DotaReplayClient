using System;
using AutoMapper;
using AutoMapper.QueryableExtensions.Impl;
using DOTAReplay.Bots;
using DOTAReplay.Bots.ReplayBot;
using DOTAReplay.Model;
using Newtonsoft.Json.Linq;
using SteamKit2.GC.Dota.Internal;

namespace DOTAReplay
{
    internal class Program
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            log.Info("DOTAReplay fetcher starting up!");

            Mapper.CreateMap<CMsgDOTAMatch, MatchResult>();

            BotDB.UpdateDB();
            var downloader = new MatchDownloader();
            Console.ReadLine();
            BotDB.Shutdown();
            downloader.timer.Dispose();
        }
    }
}
