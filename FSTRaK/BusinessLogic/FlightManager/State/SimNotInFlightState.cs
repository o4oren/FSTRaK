using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class SimNotInFlightState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }
        public SimNotInFlightState(FlightManager context): base(context) 
        {
            Name = "Not in flight";
            IsMovementState = false;

            context.CurrentFlightParams = new FlightParams();
            context.ActiveFlight = null;
        }
        public override void ProcessFlightData(FlightData data)
        {
            
            if(Context.SimConnectInFlight)
            {
                Context.State = new FlightStartedState(Context);
            }
        }

        public override void HandleFlightExitEvent()
        {
            // Do nothing
        }
    }
}
