using FSTRaK.Models;
using MapControl;
using Serilog;
using System;
using System.ComponentModel;


namespace FSTRaK.ViewModels
{
    internal class FlightDataViewModel : BaseViewModel
    {
        FlightManager flightManager = FlightManager.Instance;

        public Aircraft Aircraft
        {
            get
            {
                if (flightManager.Aircraft != null)
                    return flightManager.Aircraft;
                return null;
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
                case ("Aircraft"):
                    OnPropertyChanged("Aircraft");
                    if (_lastUpdated.AddSeconds(2) < DateTime.Now)
                    {
                        FlightPath.Add(flightManager.Aircraft.Position[0], flightManager.Aircraft.Position[1]);
                        _lastUpdated = DateTime.Now;
                        OnPropertyChanged("FlightPath");
                    }
                    break;
                case ("NearestAirport"):
                    NearestAirport = flightManager.NearestAirport;
                    OnPropertyChanged("NearestAirport");
                    break;
                default:
                    break;
            }
        }
    }
}