using System;
using System.Drawing;
using System.Windows.Forms;
using DOTAReplayClient.Properties;

namespace DOTAReplayClient
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon icon = null;
        ContextMenu menu = new ContextMenu();


        public TrayIcon()
        {
            icon = new NotifyIcon();
            icon.Text = "DOTAReplay Client";
            icon.Icon = Icon.FromHandle(Resources.Icon.GetHicon());
            icon.Visible = true;
            menu.MenuItems.Add(0, new MenuItem("Quit", OnClickQuit));
            icon.ContextMenu = menu;
        }

        private void OnClickQuit(object sender, EventArgs eventArgs)
        {
             DRClientManager.Instance.Shutdown();
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            icon = null;
        }
    }
}
