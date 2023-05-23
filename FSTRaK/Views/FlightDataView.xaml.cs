using FSTRaK.ViewModels;
using System.Windows;
using System.Windows.Input;

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
   
        private void OnMapMoveEvent(object sender, MouseButtonEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = false; // Disabled for now
        }

        private void OnCenterOnAirplaneChecked(object sender, RoutedEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = true;
        }

        private void OnCenterOnAirplaneUnChecked(object sender, RoutedEventArgs e)
        {
            if(DataContext != null)
            {
                ((FlightDataViewModel)this.DataContext).IsCenterOnAirplane = false;

            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
           // ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
        }

    }
}
