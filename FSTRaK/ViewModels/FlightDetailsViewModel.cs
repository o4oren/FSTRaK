﻿

using FSTRaK.Models;
using MapControl;
using Serilog;
using System;
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

        }
    }
}