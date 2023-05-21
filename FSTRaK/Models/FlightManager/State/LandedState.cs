

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
            AddFlightEvent(Data, new FlightEvent());

            if(!Data.simOnGround)
            {
                Context.State = new InFlightState(Context);
            }
            HandleFlightExit(Context);
        }
    }
}
