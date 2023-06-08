
using FSTRaK.DataTypes;

namespace FSTRaK.Models.FlightManager.State
{
    internal interface IFlightManagerState
    {
        string Name { get; set; }
        bool IsMovementState { get; set; }
        void ProcessFlightData(AircraftFlightData data);
        void HandleFlightExitEvent();

    }
}
