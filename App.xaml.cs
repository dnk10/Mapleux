using System.Configuration;
using System.Data;
using System.Windows;

namespace caelestia
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;

            mutex = new Mutex(
                true,
                "CaelestiaDashboardMutex",
                out createdNew);

            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }

}
