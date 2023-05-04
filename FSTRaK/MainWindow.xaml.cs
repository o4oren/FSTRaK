using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FSTRaK
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SimConnectManager smc = null;
        public MainWindow()
        {
            smc = SimConnectManager.Instance;
            InitializeComponent();
        }

        void OnLoad(object sender, RoutedEventArgs e)
        {
            smc.Initialize();

            smc.PropertyChanged += print;
        }

        private void print(object sender, PropertyChangedEventArgs e)
        {
            SimConnectManager.AircraftFlightData a = smc.FlightData;  
            Log.Information($"{a.title} is at {a.latitude:F2}:{a.longitude:F2} heading: {a.trueHeading} at alt: {a.altitude}");
        }
    }
}
