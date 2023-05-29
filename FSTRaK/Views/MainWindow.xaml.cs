using FSTRaK.Models.FlightManager;
using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FlightManager _flightManager;
        public MainWindow()
        {
            _flightManager = FlightManager.Instance;
            InitializeComponent();

            // Add to tray
            System.Windows.Forms.NotifyIcon icon = new System.Windows.Forms.NotifyIcon();
            Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Images/FSTrAk.ico")).Stream;
            icon.Icon = new System.Drawing.Icon(iconStream);
            icon.Visible = true;
            icon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            _flightManager.Initialize();

            string bingApiKey = Properties.Settings.Default.BingApiKey;
            MapControl.BingMapsTileLayer.ApiKey = bingApiKey;
        }

        private void ButtonClick_CloseApplication (object sender, RoutedEventArgs e) 
        { 
            Close();
        }

        private void ButtonClick_MinimizeApplication(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            Serilog.Log.Debug("mouse!");
            Log.Debug($"{sender.GetType()}");
            DragMove();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                this.Hide();

            base.OnStateChanged(e);
        }


        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = Properties.Settings.Default.IsAlwaysOnTop;

        }

    }
}
