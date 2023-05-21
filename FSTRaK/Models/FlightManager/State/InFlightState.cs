

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class InFlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public InFlightState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 10000;
            this.Name = "In flight";
            this.IsMovementState = true;
        }
        public override void processFlightData(AircraftFlightData Data)
        {
            if (Data.simOnGround)
            {
                Context.State = new LandedState(Context);
            }

            // TODO add code to handle specific flight events
            FlightEvent fe = new FlightEvent();

            AddFlightEvent(Data, fe);
            HandleFlightExit(Context);
        }
    }
}
