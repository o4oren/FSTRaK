using FSTRaK.Models;
using FSTRaK.Models.Entity;
using Serilog;
using Serilog.Exceptions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FSTRaK.Utils;
using Microsoft.Win32;

namespace FSTRaK
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;

        const string AppName = "FSTrAk";

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, AppName, out var createdNew);

            if (!createdNew)
            {
                MessageBox.Show("An instance of FSTrAk is already running...", "FSTrAk");
                Application.Current.Shutdown();
            }

            base.OnStartup(e);
        }


    void OnApplicationStart(object sender, StartupEventArgs args)
    {
        var logPath = Path.Combine(PathUtil.GetApplicationLocalDataPath(), "log.txt");

        Log.Logger = new LoggerConfiguration()
        .Enrich.WithExceptionDetails()
        .MinimumLevel.Information()
#if DEBUG
        .MinimumLevel.Debug()
#endif
        .WriteTo.Trace()
        .WriteTo.File(logPath)
        .CreateLogger();


        Task.Run(() =>
        {
            using (var logbookContext = new LogbookContext())
            {
                try
                {
                    logbookContext.Aircraft.Find(1);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        });

        Task.Run(() =>
        {

            if (FSTRaK.Properties.Settings.Default.IsStartAutomatically)
            {
                // Start up with windows login
                RegistryKey rkStartUp = Registry.CurrentUser;
                var applicationLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

                var startupPathSubKey = rkStartUp.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);


                AppDomain.CurrentDomain.SetData("DataDirectory", PathUtil.GetApplicationLocalDataPath());
                if (startupPathSubKey?.GetValue("FSTrAk") == null)
                {
                    startupPathSubKey?.SetValue("FSTrAk", applicationLocation, RegistryValueKind.ExpandString);
                }
            }
        });

        var airportResolver = AirportResolver.Instance;
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
            Log.Error(e.Exception, "Unhandled error occurred!");
            // Prevent default unhandled exception processing
            e.Handled = true;
        }
    }
}
