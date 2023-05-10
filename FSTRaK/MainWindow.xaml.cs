using System.Windows;

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

        void OnLoad(object sender, RoutedEventArgs e)
        {
            _flightManager.Initialize();
        }
    }
}
