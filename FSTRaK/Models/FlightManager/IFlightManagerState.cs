using FSTRaK.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FSTRaK.SimConnectService;

namespace FSTRaK.Models.FlightManager
{
    internal interface IFlightManagerState
    {
        void processFlightData(FlightManager Context, AircraftFlightData Data);
    }
}
