

using FSTRaK.DataTypes;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;

namespace FSTRaK.Models.FlightManager
{
    internal abstract class AbstractState : IFlightManagerState
    {
        public abstract void processFlightData(AircraftFlightData Data);

        public abstract string Name { get; set; }

        protected FlightManager Context;
        protected Stopwatch _stopwatch = new Stopwatch();
        protected int _eventInterval = 5000;

        public abstract bool IsMovementState { get; set; }

        public AbstractState(FlightManager Context)
        {
            this.Context = Context;
        }

        protected void HandleFlightExit(FlightManager Context)
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState(Context);
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
            if (Context.ActiveFlight.StartTime == null)
            {
                Context.ActiveFlight.StartTime = time;
            }
            fe.Altitude = data.altitude;
            fe.GroundAltitude = data.groundAltitude;
            fe.Latitude = data.latitude;
            fe.Longitude = data.longitude;
            fe.TrueHeading = data.trueHeading;
            fe.Airspeed = data.indicatedAirpeed;
            fe.GroundSpeed = data.groundVelocity;
            fe.Time = time;
            Context.ActiveFlight.FlightEvents.Add(fe);
            Log.Debug(Context.ActiveFlight.FlightEvents.Last().GetType().ToString());
        }

        protected static DateTime CalculateSimTime(AircraftFlightData data)
        {
            var day = new DateTime(data.zuluYear, data.zuluMonth, data.zuluDay, 0, 0, 0, 0, System.DateTimeKind.Utc);
            var time = day.AddSeconds(data.zuluTime);
            return time;
        }
    }
}
