

namespace FSTRaK.Models.FlightManager
{
    internal abstract class AbstractState : IFlightManagerState
    {
        public abstract void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data);

        public string Name { get; set; }

        public AbstractState()
        {
            this.Name = GetType().Name;
        }

        protected void HandleFlightExit(FlightManager Context)
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState();
            }
        }
    }
}
