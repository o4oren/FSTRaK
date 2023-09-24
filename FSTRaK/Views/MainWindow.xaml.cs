using FSTRaK.Utils;
using MapControl;
using MapControl.Caching;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using FSTRaK.BusinessLogic.FlightManager;
using Application = System.Windows.Application;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly FlightManager _flightManager;
        private readonly NotifyIcon _notifyIcon;
        public MainWindow()
        {
            _flightManager = FlightManager.Instance;
            InitializeComponent();

            // Add to tray
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            _flightManager.Initialize();

            // Tray icon
            var iconStream = Application.GetResourceStream(new Uri(@"pack://application:,,,/Resources/Images/FSTrAk.ico"))?.Stream;
            if (iconStream != null)
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
                _notifyIcon.Text = "FSTrAk";
                _notifyIcon.Visible = true;

                _notifyIcon.ContextMenuStrip = new ContextMenuStrip();

                _notifyIcon.ContextMenuStrip.ShowImageMargin = false;

                _notifyIcon.ContextMenuStrip.Items.Add("Show FSTrAk", null, (s, args) =>
                {
                    this.Show();
                    this.ShowInTaskbar = true;
                    WindowState = WindowState.Normal;
                });
                _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

                _notifyIcon.ContextMenuStrip.Items.Add("Quit", null, (s, args) => CloseMainWindow());


                _notifyIcon.DoubleClick +=
                    delegate (object s, EventArgs args)
                    {
                        this.Show();
                        this.ShowInTaskbar = true;
                        this.WindowState = WindowState.Normal;
                    };
            }


            _flightManager.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName.Equals(nameof(_flightManager.State)))
                {
                    _notifyIcon.Text = $"FSTrAk\n{_flightManager.State.Name}";
                }
            };

            // Initialize MapControl global settings
            var bingApiKey = Properties.Settings.Default.BingApiKey;
            BingMapsTileLayer.ApiKey = bingApiKey;
            ImageLoader.HttpClient.DefaultRequestHeaders.Add("User-Agent", "FSTrAk - Flight Simulator logbook and tracker");
            TileImageLoader.Cache = new SQLiteCache(PathUtil.GetApplicationLocalDataPath());

            if (Properties.Settings.Default.IsStartMinimized)
            {
                WindowState = WindowState.Minimized;
                if (Properties.Settings.Default.IsMinimizeToTray)
                {
                    this.Hide();
                    this.ShowInTaskbar = false;
                }
                else
                {
                    WindowState = WindowState.Minimized;
                }
            }
        }

        private void ButtonClick_CloseApplication (object sender, RoutedEventArgs e) 
        {
            if (Properties.Settings.Default.IsMinimizeToTray)
                this.Hide();
            else
            {
                CloseMainWindow();
            }
        }

        private void CloseMainWindow()
        {
            _notifyIcon.Dispose();
            Close();
        }

        private void ButtonClick_MinimizeApplication(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            var window = (Window)sender;
            window.Topmost = Properties.Settings.Default.IsAlwaysOnTop;
        }

    }
}
