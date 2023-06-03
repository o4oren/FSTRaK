

using FSTRaK.DataTypes;
using FSTRaK.Models;
using MapControl;
using ScottPlot;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
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
                    FlightPath = new ObservableCollection<Location>(_flight.FlightEvents
                        .OrderBy(e => e.ID)
                        .Select(e => new Location(e.Latitude, e.Longitude)));

                    double minLon = Double.MaxValue, minLat = Double.MaxValue, maxLon = Double.MinValue, maxLat = Double.MinValue;

                    FlightPath.ToList().ForEach(coords =>
                    {
                        minLon = Math.Min(minLon, coords.Longitude);
                        maxLon = Math.Max(maxLon, coords.Longitude);
                        minLat = Math.Min(minLat, coords.Latitude);
                        maxLat = Math.Max(maxLat, coords.Latitude);
                    });

                    var boundingBox = new BoundingBox(minLat, minLon, maxLat, maxLon);
                    Log.Debug($"{boundingBox.Center} {boundingBox.Width}");
                    ViewPort = boundingBox;

                    ScoreboardText = _flight.GetScoreDetails();

                    FlightParams = _flight.ToString();

                    GeneratePushpins();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FlightPath));
                    OnPropertyChanged(nameof(AltSpeedGroundAltDictionary));
                }
            } 
        }

        private void GeneratePushpins()
        {
            var markerEvents = _flight.FlightEvents.Where(e => e is ScoringEvent || e is TakeoffEvent);
            MarkerList.Clear();
            foreach (var e in markerEvents)
            {
                FlightEventPushpin pin = new FlightEventPushpin();
                if(e is ScoringEvent @event)
                {
                    if (@event.ScoreDelta < -15)
                        pin.Color = "Red";
                    else if (@event.ScoreDelta < 0)
                        pin.Color = "Yellow";


                }
                pin.Text = e.ToString();
                pin.Location = $"{e.Latitude},{e.Longitude}";
                MarkerList.Add(pin);
            }
        }

        public ObservableCollection<Location> FlightPath { get; private set; }


        private string _flightParams;
            
        public string FlightParams
        {
            get { return _flightParams; }
            private set 
            {
                _flightParams = value; 
                OnPropertyChanged();
            }
        }

        private ObservableCollection<FlightEventPushpin> _markerList = new ObservableCollection<FlightEventPushpin>();

        public ObservableCollection<FlightEventPushpin> MarkerList
        {
            get { return _markerList; }
            set
            {
                
                _markerList = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<double, double[]> AltSpeedGroundAltDictionary
        {
            get 
            {
                // Building a dictionaty where keys are the timestamp and values are arrays of groundspeed altitude and ground altitude.
                Dictionary<double, double[]> altSpeedGroundDictionary = new Dictionary<double, double[]>();
                var movementTime = _flight.FlightEvents.Where(e => e is TaxiOutEvent).FirstOrDefault(); 
                if(movementTime != null)
                {
                    var dataPoints = _flight.FlightEvents
                        .Where(e => e.Time > movementTime.Time)
                        .OrderBy(e => e.Time)
                        .GroupBy(e => (e.Time - new DateTime(1970, 1, 1))
                        .TotalMilliseconds)
                        .Select(g => g.First());
                foreach (var e in dataPoints)
                    {
                        double[] altSpeedGroundArray = new double[] { e.Altitude, e.GroundSpeed, e.GroundAltitude };
                        altSpeedGroundDictionary.Add(e.Time.ToOADate(), altSpeedGroundArray);
                    }
                }

                return altSpeedGroundDictionary;
            }
            private set { }
        }


        public string TotalFuelUsed
        {
            get
            {
                if (_flight != null)
                {
                    var totalFuelUsed = _flight.TotalFuelUsed;
                    var units = "Lbs";

                    if(Properties.Settings.Default.Units.Equals((int)Units.Metric))
                    {
                        totalFuelUsed *= DataTypes.Consts.LbsToKgs;
                        units = "Kg";
                    }

                    return $"{totalFuelUsed:F2} {units}";
                }

                return "";
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

        }

        private BoundingBox _viewPort;
        public BoundingBox ViewPort { 
            get { return _viewPort; }
            set
            {
                if (_viewPort != value) 
                {
                    _viewPort = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isShowPath = true;
        public bool IsShowPath { get 
            {
                return _isShowPath;
            } 
            set
            {
                _isShowPath = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowFlightDetails = true;
        public bool IsShowFlightDetails
        {
            get
            {
                return _isShowFlightDetails;
            }
            set
            {
                _isShowFlightDetails = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowAltSpeedCharts = false;
        public bool IsShowAltSpeedCharts
        {
            get
            {
                return _isShowAltSpeedCharts;
            }
            set
            {
                _isShowAltSpeedCharts = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowScoreboard = false;
        public bool IsShowScoreboard
        {
            get
            {
                return _isShowScoreboard;
            }
            set
            {
                _isShowScoreboard = value;
                OnPropertyChanged();
            }
        }

        private string scoreboardText;

        public string ScoreboardText 
        { 
            get => scoreboardText;
            set {
                scoreboardText = value;
                OnPropertyChanged();
            }
        }

        public class FlightEventPushpin
        {
            public string Location { get; set; }
            public string Text { get; set; } = string.Empty;

            public string Color { get; set; } = "Green";

        }
    }
}
