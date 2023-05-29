

using FSTRaK.DataTypes;
using FSTRaK.Models;
using MapControl;
using Serilog;
using System;
using System.Linq;
using System.Text;
using System.Windows;

namespace FSTRaK.ViewModels
{
    internal class FlightDetailsViewModel : BaseViewModel
    {
        private Flight _flight;
        StringBuilder _sb;
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

                    _sb.Clear()
                        .AppendLine($"Departed From: {_flight.DepartureAirport}");
                        
                    if(_flight.FlightOutcome == FlightOutcome.Crashed)
                    {
                        _sb.AppendLine(($"Crashed Near {_flight.ArrivalAirport}"));
                    } else
                    {
                        _sb.AppendLine(($"Arrived At: {_flight.ArrivalAirport}"));
                    }

                        _sb.AppendLine($"Start Time: {_flight.StartTime}")
                        .AppendLine($"End Time: {_flight.EndTime}")
                        .AppendLine($"Block Time: {_flight.FlightTime}")
                        .AppendLine($"Fuel Used: {TotalFuelUsed}")
                        .AppendLine($"Flown Distance VSI: {FlightDistance}")
                        .AppendLine($"Landing VSI: {LandingVerticalSpeed}")
                        .AppendLine($"Score: {_flight.Score}")
                        .ToString();

                    FlightParams = _sb.ToString();

                    double minLon = Double.MaxValue, minLat = Double.MaxValue, maxLon = Double.MinValue, maxLat = Double.MinValue;

                    FlightPath.ForEach(coords =>
                    {
                        minLon = Math.Min(minLon, coords.Longitude);
                        maxLon = Math.Max(maxLon, coords.Longitude);
                        minLat = Math.Min(minLat, coords.Latitude);
                        maxLat = Math.Max(maxLat, coords.Latitude);

                    });

                    var boundingBox = new BoundingBox(minLat, minLon, maxLat, maxLon);
                    Log.Debug($"{boundingBox.Center} {boundingBox.Width}");
                    ViewPort = boundingBox;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FlightPath));

                }
            } 
        }

        private string _flightParams;
            
        public string FlightParams
        {
            get { return _flightParams; }
            set 
            {
                _flightParams = value; 
                OnPropertyChanged();
            }
        }


        public string LandingVerticalSpeed
        {
            get
            {
                if(_flight != null)
                {
                    var landingEvent = (LandingEvent)_flight.FlightEvents.FirstOrDefault(e => e is LandingEvent);
                    if (landingEvent != null)
                    {
                        return $"{landingEvent.VerticalSpeed:F0} ft/m";
                    }
                }

                return "";
            }
        }

        public string FlightDistance
        {
            get
            {
                if (_flight != null)
                {
                    var distanceInNM = _flight.FlightDistanceInMeters * Consts.MetersToNauticalMiles;
                        return $"{distanceInNM:F2} NM";
                }

                return "";
            }
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
                        totalFuelUsed = totalFuelUsed * DataTypes.Consts.LbsToKgs;
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

        public LocationCollection FlightPath { get; private set; }
        public FlightDetailsViewModel()
        {
            _sb = new StringBuilder();
        }
    }
}
