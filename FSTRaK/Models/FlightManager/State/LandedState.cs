

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class LandedState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public LandedState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

        }
        public override void processFlightData(AircraftFlightData Data)
        {
            if (!Data.simOnGround)
            {
                Context.State = new InFlightState(Context);
                return;
            }

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
