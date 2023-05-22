using Serilog;
using Serilog.Events;
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
            LogEventLevel level = LogEventLevel.Information;
#if DEBUG
            level = LogEventLevel.Debug;
#endif

            Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.Trace()
            .WriteTo.File("log.txt")
            .CreateLogger();
        }

        void OnApplicationExit(object sender, ExitEventArgs e)
        {
            var smc = SimConnectService.Instance;
            if (smc != null)
            {
                smc.Close();
            }
            FSTRaK.Properties.Settings.Default.Save();
        }
    }
}
