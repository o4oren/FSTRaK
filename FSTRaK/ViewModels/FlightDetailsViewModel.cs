

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
        public Flight Flight
        {
            get { return _flight; }
            set
            {
                if (_flight == value) return;
                _flight = value;
                if (_flight == null) return;

                FlightDetailsParamsViewModel = new FlightDetailsParamsViewModel(_flight);
                OnPropertyChanged(nameof(Flight));

                // If events are already loaded, update event-dependent UI immediately
                if ((_flight.FlightEvents?.Count ?? 0) > 0)
                {
                    OnFlightEventsLoaded();
                }
                else
                {
                    // Clear stale data from previous flight while events load async
                    _altSpeedGroundAltDictionary = null;
                    FlightPath.Clear();
                    MarkerList.Clear();
                    ScoreboardText = "";
                    OnPropertyChanged(nameof(AltSpeedGroundAltDictionary));
                }
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

        internal void OnFlightEventsLoaded()
        {
            if (_flight == null) return;

            Log.Debug("OnFlightEventsLoaded: flight {FlightId}, {EventCount} events",
                _flight.Id, _flight.FlightEvents?.Count ?? 0);

            _altSpeedGroundAltDictionary = null;
            FlightPath.Clear();
            foreach (var loc in _flight.FlightEvents
                .OrderBy(e => e.Id)
                .Select(e => new Location(e.Latitude, e.Longitude)))
            {
                FlightPath.Add(loc);
            }

            Log.Debug("OnFlightEventsLoaded: FlightPath has {Count} points", FlightPath.Count);

            if (FlightPath.Count > 0)
            {
                double minLon = Double.MaxValue, minLat = Double.MaxValue, maxLon = Double.MinValue, maxLat = Double.MinValue;
                foreach (var coords in FlightPath)
                {
                    minLon = Math.Min(minLon, coords.Longitude);
                    maxLon = Math.Max(maxLon, coords.Longitude);
                    minLat = Math.Min(minLat, coords.Latitude);
                    maxLat = Math.Max(maxLat, coords.Latitude);
                }

                ViewPort = new BoundingBox(minLat, minLon, maxLat, maxLon);
            }

            ScoreboardText = _flight.GetScoreDetails();
            Log.Debug("OnFlightEventsLoaded: ScoreboardText = '{Score}'", ScoreboardText);
            FlightDetailsParamsViewModel = new FlightDetailsParamsViewModel(_flight);
            GeneratePushpins();

            OnPropertyChanged(nameof(AltSpeedGroundAltDictionary));
        }

        private void GeneratePushpins()
        {
            if (_flight?.FlightEvents == null) return;
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

        private Dictionary<double, double[]> _altSpeedGroundAltDictionary;

        public ObservableCollection<Location> FlightPath { get; private set; } = new ObservableCollection<Location>();

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
                if (_altSpeedGroundAltDictionary != null) return _altSpeedGroundAltDictionary;

                var altSpeedGroundDictionary = new Dictionary<double, double[]>();
                if (_flight?.FlightEvents == null) return altSpeedGroundDictionary;
                // Building a dictionary where keys are the timestamp and values are arrays of ground speed altitude and ground altitude.
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

                _altSpeedGroundAltDictionary = altSpeedGroundDictionary;
                return _altSpeedGroundAltDictionary;
            }
        }


        public string TotalFuelUsed
        {
            get
            {
                if (_flight == null) return "";
                return UnitsUtil.GetWeightString(_flight.TotalFuelUsed);
            }
        }

        public MapTileLayerBase MapProvider
        {
            get { return MapProviderResolver.GetMapProvider(); }
        }

        public bool IsMaptillerCMap
        {
            get => MapProvider is MapTilerMapTileLayer;
        }

        private BoundingBox _viewPort;
        public BoundingBox ViewPort
        {
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
        public bool IsShowPath
        {
            get { return _isShowPath; }
            set
            {
                _isShowPath = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowFlightDetails = true;
        public bool IsShowFlightDetails
        {
            get { return _isShowFlightDetails; }
            set
            {
                _isShowFlightDetails = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowAltSpeedCharts = false;
        public bool IsShowAltSpeedCharts
        {
            get { return _isShowAltSpeedCharts; }
            set
            {
                _isShowAltSpeedCharts = value;
                OnPropertyChanged();
            }
        }

        private bool _isShowScoreboard = false;
        public bool IsShowScoreboard
        {
            get { return _isShowScoreboard; }
            set
            {
                _isShowScoreboard = value;
                OnPropertyChanged();
            }
        }

        private string _scoreboardText;

        public string ScoreboardText
        {
            get { return _scoreboardText; }
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
