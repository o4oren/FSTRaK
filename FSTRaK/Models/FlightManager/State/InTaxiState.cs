
using FSTRaK.DataTypes;
using System.Diagnostics;

namespace FSTRaK.Models.FlightManager
{
    internal class InTaxiState : AbstractState
    {
        public override string Name { get; set; } = "Taxi";
        public override bool IsMovementState { get; set; }

        
        public InTaxiState(FlightManager Context) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Taxi";
            this.IsMovementState = true;
        }
        public override void processFlightData(AircraftFlightData Data)
        {
            if(!Data.simOnGround)
            {
                Context.State = new InFlightState(Context);
            }

            AddFlightEvent(Data, new FlightEvent());

            HandleFlightExit(Context);
        }
    }
}
