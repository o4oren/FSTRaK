﻿using System;
using System.Diagnostics;
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal abstract class AbstractState : IFlightManagerState
    {

        public abstract string Name { get; set; }

        protected BusinessLogic.FlightManager.FlightManager Context;
        protected Stopwatch Stopwatch = new Stopwatch();
        protected int EventInterval = 5000;

        public abstract bool IsMovementState { get; set; }

        protected AbstractState(BusinessLogic.FlightManager.FlightManager context)
        {
            this.Context = context;
        }

        /// <summary>
        /// This method must be implemented by inheriting class.
        /// Recommendations:
        /// 1. Handle one time operations in the constructor.
        /// 2. Use StopWatch for timed insertions of state into the db.
        /// 3. Check for special conditions at the beginning of the method.
        /// 4. Don't forget to handle exit from flight.
        /// </summary>
        /// <param name="data"></param>
        public abstract void ProcessFlightData(FlightData data);


        /// <summary>
        /// Handles the exit from flight event - detected when the sim is no longer in flight mode.
        /// </summary>
        public virtual void HandleFlightExitEvent()
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new FlightEndedState(Context);
            }
        }

        /// <summary>
        /// Receives flight data and an instance of FlightEvent. Adds the flight event to the active flight if the stopwatch is off, or if the update interval has passed.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fe"></param>
        protected void AddFlightEvent(FlightData data, BaseFlightEvent fe)
        {
            var time = CalculateSimTime(data);
            fe.Altitude = data.Altitude;
            fe.GroundAltitude = data.GroundAltitude;
            fe.Latitude = data.Latitude;
            fe.Longitude = data.Longitude;
            fe.TrueHeading = data.TrueHeading;
            fe.IndicatedAirspeed = data.IndicatedAirspeed;
            fe.GroundSpeed = data.GroundVelocity;
            fe.Time = time;
            Context.ActiveFlight.FlightEvents.Add(fe);
        }

        protected void AddFlightEvent(FlightData data)
        {
            var fe = new BaseFlightEvent();
            AddFlightEvent(data, fe);
        }

            protected static DateTime CalculateSimTime(FlightData data)
        {
            var day = new DateTime(data.zuluYear, data.zuluMonth, data.zuluDay, 0, 0, 0, 0, System.DateTimeKind.Utc);
            var time = day.AddSeconds(data.zuluTime);
            return time;
        }
    }
}
