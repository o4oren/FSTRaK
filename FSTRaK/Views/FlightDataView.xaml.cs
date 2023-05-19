using FSTRaK.ViewModels;
using MapControl;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FSTRaK.Views
{
    /// <summary>
    /// Interaction logic for FlightData.xaml
    /// </summary>
    public partial class FlightDataView : UserControl
    {
        public FlightDataView()
        {
            InitializeComponent();
            
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "ActiveFlight" && this.DataContext != null)
            {
                xMap.Center = ((FlightDataViewModel)this.DataContext).Location;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
        }

    }
}
