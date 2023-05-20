using FSTRaK.DataTypes;
using Serilog;
using System;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightEndedState : AbstractState
    {

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public FlightEndedState(FlightManager Context) : base()
        {
            this.Name = "Flight Ended";
            this.IsMovementState = false;
            Context.StopTimer();
            Log.Information($"Flight ended at {DateTime.Now}");
            // TODO persist data
        }


        public override void processFlightData(FlightManager Context, AircraftFlightData Data)
        {
            HandleFlightExit(Context);
        }
    }
}
