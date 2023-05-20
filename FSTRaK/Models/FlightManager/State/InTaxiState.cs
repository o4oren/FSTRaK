
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class InTaxiState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public InTaxiState(FlightManager Context) : base()
        {
            this.Name = "Taxi";
            this.IsMovementState = true;
            Context.SetEventTimer(5000);
        }
        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
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
