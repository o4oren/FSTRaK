﻿
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal interface IFlightManagerState
    {
        string Name { get; set; }
        bool IsMovementState { get; set; }
        void ProcessFlightData(FlightData data);
        void HandleFlightExitEvent();

    }
}
