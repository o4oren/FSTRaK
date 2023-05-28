
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal class CrashedState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }


        public CrashedState(FlightManager Context) : base(Context)
        {
            IsMovementState = false;
            Name = "Crashed";
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.CrashedNear);
        }
        public override void ProcessFlightData(AircraftFlightData Data)
        {
            CrashEvent ce = new CrashEvent();
            AddFlightEvent(Data, ce);

            Context.State = new FlightEndedState(Context);
        }
    }
}
