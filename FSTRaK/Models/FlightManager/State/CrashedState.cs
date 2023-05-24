
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class CrashedState : AbstractState
    {
        public override string Name { get; set; } = "Crashed";
        public override bool IsMovementState { get; set; }


        public CrashedState(FlightManager Context) : base(Context)
        {

        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            CrashEvent ce = new CrashEvent();
            AddFlightEvent(Data, ce);
            HandleFlightExit(Context);

            Context.State = new FlightEndedState(Context);
        }
    }
}
