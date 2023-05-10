using MapControl;
using Serilog;
using System;
using System.Windows;

namespace FSTRaK
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        void OnApplicationStart(object sender, StartupEventArgs args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Trace()
            .WriteTo.File("log.txt")
            .CreateLogger();
        }

        void OnApplicationExit(object sender, ExitEventArgs e)
        {
            var smc = SimConnectManager.Instance;
            if( smc != null )
            {
                smc.disposeSimconnect();
            }
        }
    }
}
