﻿using FSTRaK.DataTypes;
using System;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightStartedState : AbstractState
    {
        private Boolean _isStarted = false;

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }

        public FlightStartedState() : base()
        {
            this.Name = "Flight Started";
            this.IsMovementState = false;
        }

        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
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
                Context.SetEventTimer(5000);
                Context.AddFlightEvent(Data);
                Context.RequestNearestAirports(NearestAirportRequestType.Departure);
                _isStarted = true;
            } 

            // Compare the location to determine movement ONLY after out of the "ready to fly" screen
            if (Data.CameraState == (int)CameraState.Cockpit && (Data.latitude != Context.CurrentFlightParams.Latitude || Data.longitude != Context.CurrentFlightParams.Longitude))
            {
                Context.State = new InTaxiState(Context);
            }

            HandleFlightExit(Context);
        }
    }
}
