

namespace FSTRaK.Models.FlightManager
{
    internal abstract class AbstractState : IFlightManagerState
    {
        public abstract void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data);

        protected void HandleFlightExit(FlightManager Context)
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState();
            }
        }
    }
}
