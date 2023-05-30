using FSTRaK.Models.Entity;
using Serilog;
using Serilog.Exceptions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

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
            .Enrich.WithExceptionDetails()
            .MinimumLevel.Information()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.Trace()
            .WriteTo.File("log.txt")
            .CreateLogger();

            Task.Run(() =>
            {
                using (var logbookContext = new LogbookContext())
                {
                    logbookContext.Aircraft.Find(1);
                }
            });
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

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Un handled error occured!");
            

            // Prevent default unhandled exception processing
            e.Handled = true;
        }
    }
}
