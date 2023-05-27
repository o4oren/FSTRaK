

using FSTRaK.DataTypes;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;

namespace FSTRaK.Models.FlightManager
{
    internal abstract class AbstractState : IFlightManagerState
    {

        public abstract string Name { get; set; }

        protected FlightManager Context;
        protected Stopwatch _stopwatch = new Stopwatch();
        protected int _eventInterval = 5000;

        public abstract bool IsMovementState { get; set; }

        public AbstractState(FlightManager Context)
        {
            this.Context = Context;
        }

        /// <summary>
        /// This method must be implemented by inheriting class.
        /// Recommendations:
        /// 1. Handle one time operations in the constructor.
        /// 2. Use StopWatch for timed insrations of state into the db.
        /// 3. Check for special conditions at the beginning of the method.
        /// 4. Don't forget to handle exit from flight.
        /// </summary>
        /// <param name="Data"></param>
        public abstract void ProcessFlightData(AircraftFlightData Data);


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
        /// Recieves flight data and an instance of FlightEvent. Adds the flight event to the active flight if the stopwatch is off, or if the update interval has passed.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="fe"></param>
        protected void AddFlightEvent(AircraftFlightData data, FlightEvent fe)
        {
            DateTime time = CalculateSimTime(data);
            fe.Altitude = data.Altitude;
            fe.GroundAltitude = data.GroundAltitude;
            fe.Latitude = data.Latitude;
            fe.Longitude = data.Longitude;
            fe.TrueHeading = data.TrueHeading;
            fe.IndicatedAirspeed = data.IndicatedAirpeed;
            fe.GroundSpeed = data.GroundVelocity;
            fe.Time = time;
            Context.ActiveFlight.FlightEvents.Add(fe);
            Log.Debug(Context.ActiveFlight.FlightEvents.Last().GetType().ToString());
        }

        protected void AddFlightEvent(AircraftFlightData data)
        {
            FlightEvent fe = new FlightEvent();
            AddFlightEvent(data, fe);
        }

            protected static DateTime CalculateSimTime(AircraftFlightData data)
        {
            var day = new DateTime(data.zuluYear, data.zuluMonth, data.zuluDay, 0, 0, 0, 0, System.DateTimeKind.Utc);
            var time = day.AddSeconds(data.zuluTime);
            return time;
        }
    }
}
