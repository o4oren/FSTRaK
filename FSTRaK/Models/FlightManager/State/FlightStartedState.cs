using FSTRaK.DataTypes;
using Serilog;
using System;
using System.Diagnostics;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightStartedState : AbstractState
    {
        private Boolean _isStarted = false;
        private double _prevFuelQuantity = 0;
        private Stopwatch _fuelingStopwatch;

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }

        private FlightStartedEvent _flightStartedEvent;

        public FlightStartedState(FlightManager Context) : base(Context)
        {
            this.Name = "Flight Started";
            this.IsMovementState = false;
            _flightStartedEvent = new FlightStartedEvent();
            _fuelingStopwatch = new Stopwatch();            
        }

        public override void ProcessFlightData(AircraftFlightData Data)
        {
            // Only once in actual plane and not paused
            // This should only happen once per flight
            if (!_isStarted)
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
                
                Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                _isStarted = true;
            }
            
            if (Data.CameraState == (int)CameraState.Cockpit && Context.ActiveFlight.FlightEvents.Count == 0)
            {
                _prevFuelQuantity = Data.FuelWeightLbs;
                _flightStartedEvent.FuelWeightLbs = Data.FuelWeightLbs;
                _fuelingStopwatch.Start();
                AddFlightEvent(Data, _flightStartedEvent);
            }

            // Update start event's fuel quantity if it was a large change (because that indicates the user set up fuel). Small changes can be attributed to APU running.
            if(_fuelingStopwatch.ElapsedMilliseconds > 1000)
            {
                if(_prevFuelQuantity + 3 < Data.FuelWeightLbs || _prevFuelQuantity - 3 > Data.FuelWeightLbs)
                {
                    _flightStartedEvent.FuelWeightLbs = Data.FuelWeightLbs;
                    _prevFuelQuantity = Data.FuelWeightLbs;
                    Log.Debug($"Fuel Quantity updated to {_flightStartedEvent.FuelWeightLbs}");
                }
            }

            // Compare the location to determine movement ONLY after out of the "ready to fly" screen
            if (Data.CameraState == (int)CameraState.Cockpit && 
                (Data.Latitude != Context.CurrentFlightParams.Latitude || Data.Longitude != Context.CurrentFlightParams.Longitude) && Data.GroundVelocity > 1)
            {
                TaxiOutEvent to = new TaxiOutEvent
                {
                    FuelWeightLbs = Data.FuelWeightLbs
                };
                AddFlightEvent(Data, to);
                Context.State = new TaxiOutState(Context);
            }
        }
    }
}

