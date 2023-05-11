using FSTRaK.Models;
using FSTRaK.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK
{
    internal sealed class FlightManager : INotifyPropertyChanged
    {
        private static readonly object _lock = new object();
        private static FlightManager instance = null;
        private FlightManager() { }

        public event PropertyChangedEventHandler PropertyChanged;

        private SimConnectService _simConnectService;
        public static FlightManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new FlightManager();
                    }
                    return instance;
                }
            }
        }

        internal void Initialize()
        {
            _simConnectService = SimConnectService.Instance;
            _simConnectService.Initialize();
            _simConnectService.PropertyChanged += SimconnectService_OnPropertyChange;

            // TODO initialize for every new flight
            _activeFlight = new Flight();
        }

        // Properties
        private Flight _activeFlight;
        public Flight ActiveFlight
        {
            get { return _activeFlight; }
            set
            {
                if (value != _activeFlight)
                {
                    _activeFlight = value;
                    OnPropertyChanged();
                }
            }
        }

        string _nearestAirport = string.Empty;
        public string NearestAirport
        {
            get { return _nearestAirport; }
            set
            {
                if(value != _nearestAirport)
                {
                    _nearestAirport = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SimconnectService_OnPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            
            switch(e.PropertyName)
            {
                case "FlightData":
                    var data = _simConnectService.FlightData;
                    Aircraft aircraft;
                    if (ActiveFlight.Aircraft == null)
                    {
                        aircraft = new Aircraft();
                        aircraft.Title = data.title;
                        aircraft.Type = data.atcType;
                        aircraft.Model = data.model;
                        aircraft.Airline = data.airline;
                        ActiveFlight.Aircraft = aircraft;
                    }
                    // ActiveFlight.StartTime = new DateTime(Convert.ToInt64(data.absoluteTime * 1000));

                    var day = new DateTime(data.zuluYear, data.zuluMonth, data.zuluDay, 0, 0, 0, 0, System.DateTimeKind.Utc);
                    var time = day.AddSeconds(data.zuluTime);

                    FlightEvent fe = new FlightEvent();
                    fe.Altitude = data.altitude;
                    fe.Latitude = data.latitude;
                    fe.Longitude = data.longitude;
                    fe.TrueHeading = data.trueHeading;
                    fe.Airspeed = data.airspeed;
                    fe.Time = time;
                    ActiveFlight.FlightEvents.Add(fe);
                    OnPropertyChanged("ActiveFlight");


                    if(ActiveFlight.StartTime == null)
                    {
                        ActiveFlight.StartTime = time;
                    }
                    break;
                case "NearestAirport":
                    var airport = _simConnectService.NearestAirport;
                    if(ActiveFlight != null && ActiveFlight.DepartureAirport == null)
                    {
                        ActiveFlight.DepartureAirport = airport;
                    }
                    break;
            }
        }


        public void Close()
        {
            _simConnectService?.Close();
        }


        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
