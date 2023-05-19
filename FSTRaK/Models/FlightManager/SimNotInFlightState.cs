using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models.FlightManager
{
    internal class SimNotInFlightState : IFlightManagerState
    {
        public SimNotInFlightState() {
            Log.Information("State changed: Not in flight");

        }
        public void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            Context.ActiveFlight = null;
        }
    }
}
