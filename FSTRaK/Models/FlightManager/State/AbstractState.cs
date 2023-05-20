

using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal abstract class AbstractState : IFlightManagerState
    {
        public abstract void processFlightData(FlightManager Context, AircraftFlightData Data);

        public abstract string Name { get; set; }
        public abstract bool IsMovementState { get; set; }

        public AbstractState()
        {
            this.Name = GetType().Name;
        }

        protected void HandleFlightExit(FlightManager Context)
        {
            if (!Context.SimConnectInFlight)
            {
                Context.State = new SimNotInFlightState(Context);
            }
        }
    }
}
