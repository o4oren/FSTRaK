

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public FlightState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 10000;
            this.Name = "In flight";
            this.IsMovementState = true;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if (Data.SimOnGround == 1)
            {
                LandingEvent To = new LandingEvent() { VerticalSpeed = Data.VerticalSpeed };
                AddFlightEvent(Data, To);
                Context.State = new LandedState(Context);
                return;
            }

            // TODO add code to handle specific flight events

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data, new FlightEvent());
                _stopwatch.Restart();
            }
            
            HandleFlightExit(Context);
        }
    }
}
