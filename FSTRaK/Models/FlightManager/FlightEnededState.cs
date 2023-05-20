using Serilog;
using System;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightEndedState : AbstractState
    {
        public FlightEndedState(FlightManager Context) 
        {
            Context.StopTimer();
            Log.Information($"Flight ended at {DateTime.Now}");
            // TODO persist data
        }

        public override void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            HandleFlightExit(Context);
        }
    }
}
