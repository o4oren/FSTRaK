using FSTRaK.Models;
using MapControl;
using Serilog;
using System;
using System.ComponentModel;
using System.Linq;

namespace FSTRaK.ViewModels
{
    internal class FlightDataViewModel : BaseViewModel
    {
        FlightManager flightManager = FlightManager.Instance;

        public Flight ActiveFlight
        {
            get
            {
                if (flightManager.ActiveFlight != null)
                    return flightManager.ActiveFlight;
                return null;
            }
        }

        public string Details
        {
            get
            {
                if (flightManager.ActiveFlight != null)
                    return $"Flying at speed: {ActiveFlight.FlightEvents.Last().Airspeed}  altitude:{ActiveFlight.FlightEvents.Last().Altitude}   heading: {ActiveFlight.Heading} ";
                return string.Empty;
            }
        }

        public LocationCollection FlightPath { get; set; } = new LocationCollection();

        public string NearestAirport { get; set; }


        private DateTime _lastUpdated = DateTime.Now;

        public FlightDataViewModel()
        {
            flightManager.PropertyChanged += SimconnectManagerUpdate;
        }

        private void SimconnectManagerUpdate(object sender, PropertyChangedEventArgs e)
        {
            
            switch (e.PropertyName)
            {
                case ("ActiveFlight"):
                    if (_lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(flightManager.ActiveFlight.Latitude, flightManager.ActiveFlight.Longitude);
                        _lastUpdated = DateTime.Now;
                        OnPropertyChanged("FlightPath");
                        OnPropertyChanged("Details");
                    }
                    OnPropertyChanged("ActiveFlight");

                    break;

                default:
                    break;
            }
        }
    }
}