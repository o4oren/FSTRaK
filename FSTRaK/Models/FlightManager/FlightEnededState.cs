using Serilog;
using System;
using System.Runtime.Remoting.Contexts;

namespace FSTRaK.Models.FlightManager
{
    internal class FlightEndedState : IFlightManagerState
    {
        public FlightEndedState(FlightManager Context) 
        {
            Context.StopTimer();
            Log.Information($"Flight ended at {DateTime.Now}");
        }

        public void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {

        }
    }
}
