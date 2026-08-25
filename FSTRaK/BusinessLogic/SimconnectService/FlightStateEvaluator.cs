using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Everything the in-flight decision depends on, captured at one instant.
    /// </summary>
    internal struct FlightStateInputs
    {
        public CameraState Camera;
        public CameraState PreviousCamera;
        public uint PauseState;
        public string LoadedFlight;
        public bool IsConnected;
        public bool WasInFlight;
    }

    /// <summary>
    /// Decides whether the simulator is in a flight.
    ///
    /// The branch order below is load-bearing and was arrived at empirically against both
    /// MSFS 2020 and 2024; the two sims signal flight entry and exit differently, and
    /// several branches exist to suppress transient states that would otherwise read as a
    /// flight ending. Change the order only with a test that pins the behaviour you intend.
    /// </summary>
    internal static class FlightStateEvaluator
    {
        private const string MainMenuFlt = "flights\\other\\MainMenu.FLT";

        public static bool IsInFlight(FlightStateInputs inputs)
        {
            // A lost connection ends the flight regardless of the last camera seen.
            if (inputs.WasInFlight && !inputs.IsConnected)
            {
                return false;
            }

            // Active pause and the in-flight menu (including entering VR) must not end a
            // flight already under way. PauseState 8 also follows 9 when 2024 ends a flight.
            if (inputs.WasInFlight
                && (inputs.PauseState == 1 || inputs.PauseState == 8 || inputs.PauseState == 0)
                && (inputs.Camera == CameraState.InFlightMenu2024
                    || inputs.Camera == CameraState.InFlightMenu2024_2
                    || inputs.Camera == CameraState.InFlightMenu2024_3))
            {
                return true;
            }

            // A live camera means the aircraft is being flown - the 2024 entry condition.
            if (inputs.Camera == CameraState.Cockpit
                || inputs.Camera == CameraState.External
                || inputs.Camera == CameraState.Drone
                || inputs.Camera == CameraState.Fixed
                || inputs.Camera == CameraState.Environment
                || inputs.Camera == CameraState.SixDof
                || inputs.Camera == CameraState.FollowTrafficAircraft)
            {
                return true;
            }

            if (inputs.Camera == CameraState.LoadingFlight3D2024
                || inputs.Camera == CameraState.SomethingInLoadingProcess2024)
            {
                return false;
            }

            if (inputs.Camera == CameraState.MainMenu2024)
            {
                // 2024 shows the main-menu camera transiently on the way out of the
                // in-flight menu; that is not a flight ending.
                return inputs.PreviousCamera == CameraState.InFlightMenu2024_3;
            }

            if (inputs.WasInFlight && inputs.PauseState == 9)
            {
                return false;
            }

            // 2020 entry condition: a loaded flight that is not the main menu, unpaused.
            return !string.IsNullOrEmpty(inputs.LoadedFlight)
                   && !inputs.LoadedFlight.Equals(MainMenuFlt)
                   && inputs.PauseState != 1
                   && inputs.PauseState != 8
                   && inputs.PauseState != 9;
        }
    }
}
