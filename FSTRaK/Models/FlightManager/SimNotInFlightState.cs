namespace FSTRaK.Models.FlightManager
{
    internal class SimNotInFlightState : AbstractState
    {
        public SimNotInFlightState(): base() {
            Name = "Not in flight";
        }
        public override void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            Context.ActiveFlight = null;
            if(Context.SimConnectInFlight)
            {
                Context.State = new FlightStartedState();
            }
        }
    }
}
