

using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Utils;
using MapControl;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace FSTRaK.ViewModels
{
    internal class FlightDetailsViewModel : BaseViewModel
    {
        private Flight _flight;
        public Flight Flight { 
            get => _flight;
            set
            {
                if (_flight == value) return;
                _flight = value;
                FlightPath = new ObservableCollection<Location>(_flight.FlightEvents
                    .OrderBy(e => e.Id)
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

                FlightDetailsParamsViewModel = new FlightDetailsParamsViewModel(_flight);

                GeneratePushpins();

                OnPropertyChanged();
                OnPropertyChanged(nameof(FlightPath));
                OnPropertyChanged(nameof(AltSpeedGroundAltDictionary));
            } 
        }

        private FlightDetailsParamsViewModel _flightDetailsParamsViewModel;
        public FlightDetailsParamsViewModel FlightDetailsParamsViewModel
        {
            get
            {
                return _flightDetailsParamsViewModel;
            }
            private set
            {
                if (value != _flightDetailsParamsViewModel)
                {
                    _flightDetailsParamsViewModel = value;
                    OnPropertyChanged();
                }
            }
        }

        private void GeneratePushpins()
        {
            var markerEvents = _flight.FlightEvents.Where(e => e is ScoringEvent || e is TakeoffEvent).ToList();

            // Clear adjacent landings
            var landings = markerEvents.Where(e => e is LandingEvent).ToList();
            for (var i = 0; i < landings.Count; i++)
            {
                if (i <= 0) continue;
                if (landings[i].Time < landings[i - 1].Time.AddSeconds(10))
                {
                    markerEvents.Remove(landings[i]);
                }
            }

            MarkerList.Clear();
            foreach (var e in markerEvents)
            {
                var pin = new FlightEventPushpin();
                if (e is ScoringEvent @event)
                {
                    if (@event.ScoreDelta > 0)
                        pin.Color = "#82A0BC";
                    if (@event.ScoreDelta <= -20)
                        pin.Color = "Red";
                    else if (@event.ScoreDelta < 0)
                        pin.Color = "#DE970B";
                }
                pin.Text = e.ToString();
                pin.Location = $"{e.Latitude},{e.Longitude}";
                MarkerList.Add(pin);
            }
        }

        public ObservableCollection<Location> FlightPath { get; private set; }

        private ObservableCollection<FlightEventPushpin> _markerList = new ObservableCollection<FlightEventPushpin>();

        public ObservableCollection<FlightEventPushpin> MarkerList
        {
            get => _markerList;
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
                // Building a dictionary where keys are the timestamp and values are arrays of ground speed altitude and ground altitude.
                var altSpeedGroundDictionary = new Dictionary<double, double[]>();
                var movementTime = _flight.FlightEvents.FirstOrDefault(e => e is TaxiOutEvent);
                if (movementTime == null) return altSpeedGroundDictionary;
                {
                    var dataPoints = _flight.FlightEvents
                        .Where(e => e.Time > movementTime.Time)
                        .OrderBy(e => e.Time)
                        .GroupBy(e => (e.Time - new DateTime(1970, 1, 1))
                            .TotalMilliseconds)
                        .Select(g => g.First());
                    foreach (var e in dataPoints)
                    {
                        var altSpeedGroundArray = new double[] { e.Altitude, e.GroundSpeed, e.GroundAltitude };
                        altSpeedGroundDictionary.Add(e.Time.ToOADate(), altSpeedGroundArray);
                    }
                }

                return altSpeedGroundDictionary;
            }
        }


        public string TotalFuelUsed
        {
            get
            {
                if (_flight == null) return "";
                var totalFuelUsed = _flight.TotalFuelUsed;
                var units = "Lbs";

                if (!Properties.Settings.Default.Units.Equals((int)Units.Metric)) return $"{totalFuelUsed:F2} {units}";
                totalFuelUsed *= DataTypes.Consts.LbsToKgs;
                units = "Kg";

                return $"{totalFuelUsed:F2} {units}";
            }
        }

        public MapTileLayerBase MapProvider
        {
            get
            {
                var resoueceKey = Properties.Settings.Default.MapTileProvider;
                var resource = Application.Current.Resources[resoueceKey] as MapTileLayerBase;
                if (resource != null)
                {
                    if (resource.SourceName.StartsWith("SkyVector"))
                    {
                        resource.TileSource = new SkyVectorTileSource
                        {
                            UriTemplate = resource.TileSource.UriTemplate,
                        };
                    }

                    return resource;
                }

                return Application.Current.Resources["OpenStreetMap"] as MapTileLayerBase;
            }
        }

        private BoundingBox _viewPort;
        public BoundingBox ViewPort { 
            get => _viewPort;
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
        public bool IsShowPath { get => _isShowPath;
            set
            {
                _isShowPath = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowFlightDetails = true;
        public bool IsShowFlightDetails
        {
            get => _isShowFlightDetails;
            set
            {
                _isShowFlightDetails = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowAltSpeedCharts = false;
        public bool IsShowAltSpeedCharts
        {
            get => _isShowAltSpeedCharts;
            set
            {
                _isShowAltSpeedCharts = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowScoreboard = false;
        public bool IsShowScoreboard
        {
            get => _isShowScoreboard;
            set
            {
                _isShowScoreboard = value;
                OnPropertyChanged();
            }
        }

        private string _scoreboardText;

        public string ScoreboardText 
        { 
            get => _scoreboardText;
            set {
                _scoreboardText = value;
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
