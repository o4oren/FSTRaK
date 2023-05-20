using FSTRaK.Models;
using FSTRaK.Models.FlightManager;
using MapControl;
using System;
using System.ComponentModel;
using System.Linq;
using static FSTRaK.Models.FlightManager.FlightManager;

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

        private string _title;
        public string Title
        {
            get { return _title; }
            set 
            { 
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _model;
        public string Model
        {
            get { return _model; }
            set
            {
                _model = value;
                OnPropertyChanged();
            }
        }

        private string _airline;
        public string Airline
        {
            get { return _airline; }
            set
            {
                _airline = value;
                OnPropertyChanged();
            }
        }

        private string _type;
        public string Type
        {
            get { return _type; }
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        public Location Location
        {
            get
            {
                if (flightManager != null)
                    return new Location(flightManager.CurrentFlightParams.Latitude, flightManager.CurrentFlightParams.Longitude);
                return new Location(0,0);
            }
            private set
            { }
        }


        public string FlightParamsText
        {
            get
            {
                if (flightManager != null)
                {
                    return $"Airspeed: {flightManager.CurrentFlightParams.TrueAirspeed:F0} Kts\nAltitude: {flightManager.CurrentFlightParams.Altitude:F0} Ft\nHeading: {flightManager.CurrentFlightParams.Heading:F0} Deg" +
                        $"\nPosition: {flightManager.CurrentFlightParams.Latitude:F4},{flightManager.CurrentFlightParams.Longitude:F4}";
                }
                return "";
            }
        }


        public double Heading
        {
            get
            {
                if (flightManager != null)
                    return flightManager.CurrentFlightParams.Heading;
                return 0;
            }
            private set
            { }
        }

        public LocationCollection FlightPath { get; set; } = new LocationCollection();

        public LocationCollection LastSegmentLine { get 
            {
                if(FlightPath.Count > 0)
                {
                    return new LocationCollection(FlightPath.Last(), Location);
                }
                return new LocationCollection();
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
                    if (!(flightManager.State is SimNotInFlightState) && _lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(flightManager.CurrentFlightParams.Latitude, flightManager.CurrentFlightParams.Longitude);
                        _lastUpdated = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(flightManager.ActiveFlight));
                    OnPropertyChanged(nameof(Location));

                    // Begining of flight
                    if(flightManager.State is FlightStartedState && ActiveFlight != null)
                    {
                        Title = $"Aircraft: {ActiveFlight.Aircraft.Title}";
                        Model = $"Model: {ActiveFlight.Aircraft.Model}";
                        Type = $"Type: {ActiveFlight.Aircraft.Type}";
                        Airline = $"Airline: {ActiveFlight.Aircraft.Airline}";
                    }
                    OnPropertyChanged(nameof(LastSegmentLine));
                    break;

                // Send property updates for calculated fields
                case nameof(flightManager.CurrentFlightParams):
                    OnPropertyChanged(nameof(Heading));
                    OnPropertyChanged(nameof(Location));
                    OnPropertyChanged(nameof(FlightParamsText));

                    break;

                case nameof(flightManager.State):
                    FlightPath.Clear();
                    OnPropertyChanged(nameof(FlightPath));
                    OnPropertyChanged(nameof(flightManager.State));

                    break;

                default:
                    break;
            }
        }
    }
}