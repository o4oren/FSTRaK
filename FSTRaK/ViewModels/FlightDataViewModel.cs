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

        public string Location { get {
                if (flightManager.ActiveFlight != null)
                    return $"{ActiveFlight.Latitude},{ActiveFlight.Longitude}";
                return ("0, 0");
            }
            set { }
                }

        public string Details
        {
            get
            {
                if (flightManager.ActiveFlight != null && ActiveFlight.FlightEvents.Count > 0)
                    return $"Flying at speed: {ActiveFlight.FlightEvents.Last().Airspeed:F0}  altitude: {ActiveFlight.FlightEvents.Last().Altitude:N0}   heading: {ActiveFlight.Heading:F0} ";
                return string.Empty;
            }
        }

        public bool IsInFlight
        {
            get
            {
                return flightManager.IsInFlight;
            }
        }

        public LocationCollection FlightPath { get; set; } = new LocationCollection();

        public LocationCollection LastSegmentLine { get 
            {
                return new LocationCollection(FlightPath.Last(),  new MapControl.Location(ActiveFlight.Latitude, ActiveFlight.Longitude));
            }
            set { } }

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
                case nameof(flightManager.ActiveFlight):
                    if (flightManager.IsInFlight && _lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(flightManager.ActiveFlight.Latitude, flightManager.ActiveFlight.Longitude);
                        _lastUpdated = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(flightManager.ActiveFlight));
                    OnPropertyChanged(nameof(Details));
                    OnPropertyChanged(nameof(Location));
                    OnPropertyChanged(nameof(LastSegmentLine));
                    // TODO replace these with dependency propeties

                    break;

                case nameof(flightManager.IsInFlight):
                    FlightPath.Clear();
                    OnPropertyChanged(nameof(FlightPath));
                    OnPropertyChanged(nameof(IsInFlight));

                    break;

                default:
                    break;
            }
        }
    }
}