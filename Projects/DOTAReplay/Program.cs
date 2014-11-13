using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DOTAReplay.Bots.ReplayBot;
using DOTAReplay.Model;

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
            var bot =
                new ReplayBot(new Bot()
                {
                    Id = "test",
                    InUse = true,
                    Invalid = false,
                    Password = "dotacinema23",
                    Username = "dcarena23"
                });
            bot.Start();
            Console.ReadLine();
            bot.Destroy();
        }
    }
}
