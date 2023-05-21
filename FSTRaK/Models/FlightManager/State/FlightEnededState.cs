using FSTRaK.DataTypes;
using Serilog;
using System;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightEndedState : AbstractState
    {

        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public FlightEndedState(FlightManager Context) : base(Context)
        {
            this.Name = "Flight Ended";
            this.IsMovementState = false;
            this._stopwatch.Stop();
            Log.Information($"Flight ended at {DateTime.Now}");
            // TODO persist data
        }


        public override void processFlightData(AircraftFlightData Data)
        {
            HandleFlightExit(Context);
        }
    }
}
