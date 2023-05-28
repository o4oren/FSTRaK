using FSTRaK.Models.FlightManager;
using Serilog;
using System;
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

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            Serilog.Log.Debug("mouse!");
            Log.Debug($"{sender.GetType()}");
            DragMove();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            window.Topmost = Properties.Settings.Default.IsAlwaysOnTop;

        }

    }
}
