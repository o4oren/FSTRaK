using FSTRaK.ViewModels;
using MapControl;
using System.Linq;
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
            ((FlightDataViewModel)this.DataContext).PropertyChanged += ViewModel_PropertyChanged;
            BingMapsTileLayer.ApiKey = "tA9ckycf17b2AvkcCkGx~QFhxDoRj6fSQE2iVPdKAyA~AjHfAPGjEwrXfQmlmiqi5aCsXnRyBXPXUxm01K0y6wBWejxkHELcftncx824O6Kg";
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "ActiveFlight")
            {
                xMap.Center = new Location(((FlightDataViewModel)this.DataContext).ActiveFlight.Latitude, ((FlightDataViewModel)this.DataContext).ActiveFlight.Longitude);
            }
        }
    }
}
