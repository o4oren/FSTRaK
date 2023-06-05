using System;
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager.State
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
        public override void ProcessFlightData(AircraftFlightData data)
        {

            if (!Convert.ToBoolean(data.SimOnGround))
            {
                var to = new TakeoffEvent() { FlapsPosition = data.FlapPosition, FuelWeightLbs = data.FuelWeightLbs };
                AddFlightEvent(data, to);
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
