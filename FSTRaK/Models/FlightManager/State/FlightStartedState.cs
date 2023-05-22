using FSTRaK.DataTypes;
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightStartedState : AbstractState
    {
        private Boolean _isStarted = false;

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }

        public FlightStartedState(FlightManager Context) : base(Context)
        {
            this.Name = "Flight Started";
            this.IsMovementState = false;
        }

        public override void processFlightData(AircraftFlightData Data)
        {
            // Only once in actual plane and not paused
            // This should only happen once per flight
            if (!_isStarted && Data.CameraState == (int)CameraState.Cockpit)
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
                AddFlightEvent(Data, new FlightEvent()); // TODO - replace with a FLightStarted Event.
                Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                _isStarted = true;
            } 

            // Compare the location to determine movement ONLY after out of the "ready to fly" screen
            if (Data.CameraState == (int)CameraState.Cockpit && 
                ((Data.latitude != Context.CurrentFlightParams.Latitude || Data.longitude != Context.CurrentFlightParams.Longitude) && Data.groundVelocity > 0)
                )
            {
                Context.State = new InTaxiState(Context);
            }

            HandleFlightExit(Context);
        }
    }
}

