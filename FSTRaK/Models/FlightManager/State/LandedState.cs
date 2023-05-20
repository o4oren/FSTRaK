

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class LandedState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public LandedState(FlightManager Context) : base()
        {
            this.Name = "Landed";
            this.IsMovementState = true;
            Context.SetEventTimer(5000);
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

        }
        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
        {
            Context.AddFlightEvent(Data);

            if(!Data.simOnGround)
            {
                Context.State = new InFlightState(Context);
            }
            HandleFlightExit(Context);
        }
    }
}
