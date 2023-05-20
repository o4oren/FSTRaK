

namespace FSTRaK.Models.FlightManager
{
    internal class InFlightState : AbstractState
    {

        public InFlightState(FlightManager Context) 
        {
            Context.SetEventTimer(5000);
        }
        public override void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            if (Data.simOnGround)
            {
                Context.State = new LandedState(Context);
            }
            Context.AddFlightEvent(Data);
            HandleFlightExit(Context);
        }
    }
}
