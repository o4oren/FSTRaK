using FSTRaK.DataTypes;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.Models.FlightManager
{
    internal class SimNotInFlightState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public SimNotInFlightState(FlightManager Context): base() 
        {
            Name = "Not in flight";
            IsMovementState = false;

            Context.CurrentFlightParams = new FlightParams();
            Context.ActiveFlight = null;
        }
        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
        {
            
            if(Context.SimConnectInFlight)
            {
                Context.State = new FlightStartedState();
            }
        }
    }
}
