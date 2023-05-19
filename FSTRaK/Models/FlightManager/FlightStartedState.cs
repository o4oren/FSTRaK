using Serilog;
using System;
using System.Windows.Markup;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightStartedState : IFlightManagerState
    {
        private Boolean _isStarted = false;

        public void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            // This should only happen once per flight
            if(!_isStarted)
            {
                Flight flight = new Flight();
                Aircraft aircraft;
                aircraft = new Aircraft
                {
                    Title = Data.title,
                    Type = Data.atcType,
                    Model = Data.model,
                    Airline = Data.airline
                };
                flight.Aircraft = aircraft;
                Context.ActiveFlight = flight;
                Context.SetEventTimer(5000);

                Context.AddFlightEvent(Data);
                Log.Information($"Flight started at {DateTime.Now}");
                _isStarted = true;
            }
        }
    }
}
