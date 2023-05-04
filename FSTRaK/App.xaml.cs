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
    }
}
