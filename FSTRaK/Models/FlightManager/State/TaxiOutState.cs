
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager.State
{
    internal class TaxiOutState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        
        public TaxiOutState(FlightManager context) : base(context)
        {
            this.EventInterval = 10000;
            this.Name = "Taxi Out";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(FlightData data)
        {
            if (Context.ActiveFlight.Aircraft!= null && (data.GroundVelocity > 40 && data.MinThrottlePosition(Context.ActiveFlight.Aircraft.NumberOfEngines) > 75) || data.SimOnGround != 1)
            {
                Context.State = new TakeoffRollState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if(!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                AddFlightEvent(data, new BaseFlightEvent());
                Stopwatch.Restart();
            }
        }
    }
}
