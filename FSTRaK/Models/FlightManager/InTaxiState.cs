using Serilog;
using System;
using System.Windows.Markup;

namespace FSTRaK.Models.FlightManager
{
    internal class InTaxiState : IFlightManagerState
    {
        public void processFlightData(FlightManager Context, SimConnectService.AircraftFlightData Data)
        {
            Context.SetEventTimer(5000);
            Context.AddFlightEvent(Data);
        }
    }
}
