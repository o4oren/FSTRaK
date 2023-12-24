using System;
using System.Diagnostics;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class FlightStartedState : AbstractState
    {
        private Boolean _isStarted = false;
        private double _prevFuelQuantity = 0;
        private readonly Stopwatch _fuelingStopwatch;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private readonly FlightStartedEvent _flightStartedEvent;

        public FlightStartedState(FlightManager context) : base(context)
        {
            this.Name = "Flight Started";
            this.IsMovementState = false;
            _flightStartedEvent = new FlightStartedEvent();
            _fuelingStopwatch = new Stopwatch();            
        }

        public override void ProcessFlightData(FlightData data)
        {
            // Only once in actual plane and not paused
            // This should only happen once per flight
            if (!_isStarted)
            {
                using var logbookContext = new LogbookContext();
                var flight = logbookContext.Flights.Create();
                Context.ActiveFlight = flight;
                Context.RequestLoadedAircraft();
                Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                _isStarted = true;
                logbookContext.Dispose();
                Log.Information("Flight started!");
            }
            
            if (IsCameraLive(data.CameraState) && Context.ActiveFlight.FlightEvents.Count == 0)
            {
                _prevFuelQuantity = data.FuelWeightLbs;
                _flightStartedEvent.FuelWeightLbs = data.FuelWeightLbs;
                _fuelingStopwatch.Start();
                AddFlightEvent(data, _flightStartedEvent);
                Context.ActiveFlight.StartTime = _flightStartedEvent.Time;
            }

            // Update start event's fuel quantity if it was a large change (because that indicates the user set up fuel). Small changes can be attributed to APU running.
            if(_fuelingStopwatch.ElapsedMilliseconds > 1000)
            {
                if(_prevFuelQuantity > data.FuelWeightLbs + 5 || _prevFuelQuantity + 30 < data.FuelWeightLbs)
                {
                    _flightStartedEvent.FuelWeightLbs = data.FuelWeightLbs;
                    
                    Log.Information($"Fuel Quantity updated to {_flightStartedEvent.FuelWeightLbs}");
                }
                _prevFuelQuantity = data.FuelWeightLbs;
            }

            // Compare the location to determine movement ONLY after out of the "ready to fly" screen
            if ( IsCameraLive(data.CameraState) &&
                (Math.Abs(data.Latitude - Context.CurrentFlightParams.Latitude) > 0.0000001 || Math.Abs(data.Longitude - Context.CurrentFlightParams.Longitude) > 0.0000001) && data.GroundVelocity > 1)
            {
                var to = new TaxiOutEvent
                {
                    FuelWeightLbs = data.FuelWeightLbs
                };
                AddFlightEvent(data, to);
                Log.Information($"Taxi out! with {to.FuelWeightLbs} Lbs of fuel.");

                Context.State = new TaxiOutState(Context);
            }
        }

        private bool IsCameraLive(CameraState currentCameraState)
        {
            return currentCameraState is CameraState.Cockpit or CameraState.Drone or CameraState.External
                or CameraState.Drone;
        }

    }
}

