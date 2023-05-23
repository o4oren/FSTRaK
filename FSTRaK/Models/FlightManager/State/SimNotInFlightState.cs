using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class SimNotInFlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public SimNotInFlightState(FlightManager Context): base(Context) 
        {
            Name = "Not in flight";
            IsMovementState = false;

            Context.CurrentFlightParams = new FlightParams();
            Context.ActiveFlight = null;
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            
            if(Context.SimConnectInFlight)
            {
                Context.State = new FlightStartedState(Context);
            }
        }
    }
}
