using System.Windows;

namespace DOTAReplayClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DRClientManager system = new DRClientManager();
            base.OnStartup(e);
            system.Start();
        }
    }
}