

using FSTRaK.Models;
using MapControl;
using System.Linq;
using System.Windows;

namespace FSTRaK.ViewModels
{
    internal class FlightDetailsViewModel : BaseViewModel
    {
        private Flight _flight;
        public Flight Flight { 
            get 
            {
                return _flight;
            } 
            set 
            {
                if (_flight != value)
                {
                    _flight = value;
                    FlightPath = new LocationCollection(_flight.FlightEvents.Select(e => new Location(e.Latitude, e.Longitude)));
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FlightPath));
                }
            } 
        }

        public MapTileLayerBase MapProvider
        {
            get
            {
                string resoueceKey = Properties.Settings.Default.MapTileProvider;
                var resource = Application.Current.Resources[resoueceKey] as MapTileLayerBase;
                if (resource != null)
                {
                    return resource;
                }
                return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
            }
            private set { }
        }

        public LocationCollection FlightPath { get; private set; }
        public FlightDetailsViewModel()
        {
            
        }
    }
}
