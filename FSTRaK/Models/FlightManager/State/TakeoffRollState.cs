

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class TakeoffRollState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public TakeoffRollState
            (FlightManager Context) : base(Context)
        {
            this._eventInterval = 1000;
            this.Name = "Takeoff Roll";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {

            if (!Data.simOnGround)
            {
                TakeoffEvent To = new TakeoffEvent() { FlapsPosition = Data.FlapPosition };
                AddFlightEvent(Data, To);
                Context.State = new FlightState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data);
                _stopwatch.Restart();
            }
            
            HandleFlightExit(Context);
        }
    }
}
