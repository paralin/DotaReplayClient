using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DOTAReplayClient
{
    public class DRClientManager
    {
        public static DRClientManager Instance;
        private TrayIcon icon;
        private LoadingWindow window;
        private DRClient client;

        public DRClientManager()
        {
            Instance = this;
            icon = new TrayIcon();
            Application.Current.Exit += (sender, args) => Shutdown(false);
        }

        public void Shutdown(bool doexit = true)
        {
            if (icon != null)
            {
                icon.Dispose();
                icon = null;
            }
            if(doexit)
                Application.Current.Shutdown();
        }

        public void Start()
        {
            log4net.Config.XmlConfigurator.Configure();
            window = new LoadingWindow();
            window.Closed += (o, args) => Shutdown();
            window.Show();
            client = new DRClient();
            client.Start();
        }
    }
}
