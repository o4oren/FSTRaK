
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class TaxiOutState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }

        
        public TaxiOutState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Taxi Out";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if ((Data.GroundVelocity > 40 && Data.MinThrottlePosition() > 75) || Data.SimOnGround != 1)
            {
                Context.State = new TakeoffRollState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if(!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data, new BaseFlightEvent());
                _stopwatch.Restart();
            }
        }
    }
}
