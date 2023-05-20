using FSTRaK.ViewModels;
using Serilog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.UI.Xaml.Controls;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class FlightDataView : System.Windows.Controls.UserControl
    {
        public FlightDataView()
        {
            InitializeComponent();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

        switch (e.PropertyName)
            {
                case "ActiveFlight":
                    if (((FlightDataViewModel)this.DataContext).IsCenterOnAirplane)
                    {
                        xMap.Center = ((FlightDataViewModel)this.DataContext).Location;
                    }
                    break;
            }
        }

        private void OnMapMoveEvent(object sender, MouseButtonEventArgs e)
        {
            // ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = false; // Disabled for now
        }

        private void OnCenterOnAirplaneChecked(object sender, RoutedEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = true;
        }

        private void OnCenterOnAirplaneUnChecked(object sender, RoutedEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = false;
        }


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
        }

    }
}
