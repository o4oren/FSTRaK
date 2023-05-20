
namespace FSTRaK.Models.FlightManager
{
    internal class InTaxiState : AbstractState
    {

        public InTaxiState(FlightManager Context) : base()
        {
            this.Name = "Taxi";
            Context.SetEventTimer(5000);
        }
        public override void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            if(!Data.simOnGround)
            {
                Context.State = new InFlightState(Context);
            }
            Context.AddFlightEvent(Data);
            HandleFlightExit(Context);
        }
    }
}
