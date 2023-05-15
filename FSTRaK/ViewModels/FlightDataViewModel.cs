using FSTRaK.Models;
using MapControl;
using Serilog;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Animation;
using Windows.UI.Xaml.Automation.Peers;

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
                    return new Location(flightManager.Latitude, flightManager.Longitude);
                return new Location(0,0);
            }
            private set
            { }
        }

        public string CurrentPosition
        {
            get
            {
                if (flightManager != null)
                    return $"Location: {flightManager.Latitude:F4},{flightManager.Longitude:F4}";
                return ("0, 0");
            }
            private set
            { }
        }

        public string Speed
        {
            get
            {
                if (flightManager != null)
                    return $"Airspeed: {flightManager.Speed:F0}";
                return ("0, 0");
            }
            private set
            { }
        }

        public string Altitude
        {
            get
            {
                if (flightManager != null)
                    return $"Altitude: {flightManager.Altitude:F0}";
                return ("0, 0");
            }
            private set
            { }
        }

        public double Heading
        {
            get
            {
                if (flightManager != null)
                    return flightManager.Heading;
                return 0;
            }
            private set
            { }
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
                    if (flightManager.IsInFlight && _lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(flightManager.Latitude, flightManager.Longitude);
                        _lastUpdated = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(flightManager.ActiveFlight));
                    OnPropertyChanged(nameof(Location));

                    // Begining of flight
                    if(FlightPath.Count>0)
                    {
                        Title = $"Aircraft: {ActiveFlight.Aircraft.Title}";
                        Model = $"Model: {ActiveFlight.Aircraft.Model}";
                        Type = $"Type: {ActiveFlight.Aircraft.Type}";
                        Airline = $"Airline: {ActiveFlight.Aircraft.Airline}";
                        OnPropertyChanged(nameof(LastSegmentLine));
                    }
                    // TODO replace these with dependency propeties

                    break;

                case nameof(flightManager.Heading):
                    OnPropertyChanged(nameof(Heading));
                    break;
                case nameof(flightManager.Altitude):
                    OnPropertyChanged(nameof(flightManager.Altitude));
                    break;
                case nameof(flightManager.Speed):
                    OnPropertyChanged(nameof(flightManager.Speed));
                    break;
                case nameof(flightManager.Latitude):
                    OnPropertyChanged(nameof(CurrentPosition));
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