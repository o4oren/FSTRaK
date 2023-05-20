

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class InFlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public InFlightState(FlightManager Context) : base()
        {
            this.Name = "In flight";
            this.IsMovementState = true;
            Context.SetEventTimer(5000);
        }
        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
        {
            if (Data.simOnGround)
            {
                Context.State = new LandedState(Context);
            }
            Context.AddFlightEvent(Data);
            HandleFlightExit(Context);
        }
    }
}
