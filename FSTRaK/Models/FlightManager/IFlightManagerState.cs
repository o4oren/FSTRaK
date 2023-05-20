
using static FSTRaK.SimConnectService;

namespace FSTRaK.Models.FlightManager
{
    internal interface IFlightManagerState
    {
        void processFlightData(FlightManager Context, AircraftFlightData Data);
    }
}
