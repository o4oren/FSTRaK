using System;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class TakeoffRollState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }
        public TakeoffRollState
            (FlightManager context) : base(context)
        {
            this.EventInterval = 5000;
            this.Name = "Takeoff Roll";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(FlightData data)
        {

            if (!Convert.ToBoolean(data.SimOnGround))
            {
                var to = new TakeoffEvent() { FlapsPosition = data.FlapPosition, FuelWeightLbs = data.FuelWeightLbs };
                AddFlightEvent(data, to);
                Context.ActiveFlight.TotalPayloadLbs =
                    data.TotalWeightLbs - data.FuelWeightLbs - Context.ActiveFlight.Aircraft.EmptyWeightLbs;
                Log.Information($"Take off! Flaps: {to.FlapsPosition}, with {to.FuelWeightLbs} Lbs of fuel, and {Context.ActiveFlight.TotalPayloadLbs} lbs of payload!");

                Context.State = new FlightState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                AddFlightEvent(data);
                Stopwatch.Restart();
            }
        }
    }
}
