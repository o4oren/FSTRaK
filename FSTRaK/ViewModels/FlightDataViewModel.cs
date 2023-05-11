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
        public string Title {
            get
            {
                if(flightManager.Aircraft != null)
                    return flightManager.Aircraft.Title;
                return "";
            }
        }

        public string Model
        {
            get
            {
                if (flightManager.Aircraft != null)
                    return flightManager.Aircraft.Model;
                return "";
            }
        }

        public string Manufacturer
        {
            get
            {
                if (flightManager.Aircraft != null)
                    return flightManager.Aircraft.Manufacturer;
                return "";
            }
        }

        public string Airline
        {
            get
            {
                if (flightManager.Aircraft != null)
                    return flightManager.Aircraft.Airline;
                return "";
            }
        }

        public double[] Position { get; set; } = new double[0];
        public string Location { 
            get
            {
                if (Position.Length > 0)
                    return $"({Position[0]},{Position[1]})";
                return "0,0";

            } 
        }
        public LocationCollection FlightPath { get; set; } = new LocationCollection();
        public string Details { get; set; }
        public double Heading { get; set; }

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
                    OnPropertyChanged("Title");
                    OnPropertyChanged("Model");
                    OnPropertyChanged("Manufacturer");
                    OnPropertyChanged("Airline");

                    // TODO should be replaced
                    Details = $"Lat: {flightManager.Aircraft.Position[0]:F4} Lon:{flightManager.Aircraft.Position[1]:F4} \nHeading: {flightManager.Aircraft.Heading:F0} Alt: {flightManager.Aircraft.Altitude:F0} ft\nSpeed: {flightManager.Aircraft.Airspeed:F0} Knots";
                    Position = flightManager.Aircraft.Position;
                    Heading = flightManager.Aircraft.Heading;


                    OnPropertyChanged("Details");
                    OnPropertyChanged("Position");

                    OnPropertyChanged("Heading");
                    OnPropertyChanged("FlightData");

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