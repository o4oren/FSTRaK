

namespace FSTRaK.Models.FlightManager
{
    internal class LandedState : AbstractState
    {

        public LandedState(FlightManager Context) : base()
        {
            this.Name = "Landed";
            Context.SetEventTimer(5000);
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

        }
        public override void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            Context.AddFlightEvent(Data);
            HandleFlightExit(Context);
        }
    }
}
