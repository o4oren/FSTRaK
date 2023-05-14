using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FSTRaK
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

    }
}
