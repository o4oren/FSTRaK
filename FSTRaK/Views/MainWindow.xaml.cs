using FSTRaK.Models.FlightManager;
using MapControl;
using MapControl.Caching;
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
        private FlightManager _flightManager;
        public MainWindow()
        {
            _flightManager = FlightManager.Instance;
            InitializeComponent();
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            _flightManager.Initialize();

            string bingApiKey = Properties.Settings.Default.BingApiKey;
            BingMapsTileLayer.ApiKey = bingApiKey;
        }

        private void ButtonClick_CloseApplication (object sender, RoutedEventArgs e) 
        { 
            Close();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            Serilog.Log.Debug("mouse!");
            DragMove();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if(Properties.Settings.Default.AlwaysOnTop)
            {
                Window window = (Window)sender;
                window.Topmost = true;
            }
        }

    }
}
