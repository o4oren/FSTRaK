
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager
{
    internal interface IFlightManagerState
    {
        string Name { get; set; }
        bool IsMovementState { get; set; }
        void processFlightData(AircraftFlightData Data);
    }
}
