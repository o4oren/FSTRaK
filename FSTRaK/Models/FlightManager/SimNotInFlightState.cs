namespace FSTRaK.Models.FlightManager
{
    internal class SimNotInFlightState : IFlightManagerState
    {
        public void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            Context.ActiveFlight = null;
            if(Context.SimConnectInFlight)
            {
                Context.State = new FlightStartedState();
            }
        }
    }
}
