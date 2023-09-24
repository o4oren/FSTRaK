
using FSTRaK.DataTypes;
using FSTRaK.Models;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class CrashedState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }


        public CrashedState(FlightManager context) : base(context)
        {
            IsMovementState = false;
            Name = "Crashed";
            context.RequestNearestAirports(DataTypes.NearestAirportRequestType.CrashedNear);
        }
        public override void ProcessFlightData(FlightData data)
        {
            var ce = new CrashEvent();
            AddFlightEvent(data, ce);

            Context.State = new FlightEndedState(Context);
        }
    }
}
