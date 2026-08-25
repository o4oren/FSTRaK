using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using Xunit;

namespace FSTRaK.Tests
{
    public class FlightStateEvaluatorTests
    {
        private const string MainMenuFlt = "flights\\other\\MainMenu.FLT";

        private static FlightStateInputs Inputs(
            CameraState camera = CameraState.Cockpit,
            CameraState previousCamera = CameraState.Cockpit,
            uint pauseState = 0,
            string loadedFlight = "flights\\other\\SomeFlight.FLT",
            bool isConnected = true,
            bool wasInFlight = false)
        {
            return new FlightStateInputs
            {
                Camera = camera,
                PreviousCamera = previousCamera,
                PauseState = pauseState,
                LoadedFlight = loadedFlight,
                IsConnected = isConnected,
                WasInFlight = wasInFlight
            };
        }

        [Theory]
        [InlineData(CameraState.Cockpit)]
        [InlineData(CameraState.External)]
        [InlineData(CameraState.Drone)]
        [InlineData(CameraState.Fixed)]
        [InlineData(CameraState.Environment)]
        [InlineData(CameraState.SixDof)]
        [InlineData(CameraState.FollowTrafficAircraft)]
        public void LiveCamera_IsInFlight(CameraState camera)
        {
            Assert.True(FlightStateEvaluator.IsInFlight(Inputs(camera: camera)));
        }

        [Theory]
        [InlineData(CameraState.LoadingFlight3D2024)]
        [InlineData(CameraState.SomethingInLoadingProcess2024)]
        public void LoadingCamera_IsNotInFlight(CameraState camera)
        {
            Assert.False(FlightStateEvaluator.IsInFlight(Inputs(camera: camera)));
        }

        [Fact]
        public void MainMenu2024_EndsFlight()
        {
            var inputs = Inputs(
                camera: CameraState.MainMenu2024,
                previousCamera: CameraState.Cockpit,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void MainMenu2024_FromInFlightMenu_DoesNotEndFlight()
        {
            // Guards a 2024 quirk: the main-menu camera appears transiently when
            // leaving the in-flight menu, and must not be read as ending the flight.
            var inputs = Inputs(
                camera: CameraState.MainMenu2024,
                previousCamera: CameraState.InFlightMenu2024_3,
                wasInFlight: true);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Theory]
        [InlineData(CameraState.InFlightMenu2024, 1u)]
        [InlineData(CameraState.InFlightMenu2024_2, 8u)]
        [InlineData(CameraState.InFlightMenu2024_3, 0u)]
        public void InFlightMenuWhileInFlight_StaysInFlight(CameraState camera, uint pauseState)
        {
            // Entering VR or active pause mid-flight must not end the flight.
            var inputs = Inputs(camera: camera, pauseState: pauseState, wasInFlight: true);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void PauseState9WhileInFlight_EndsFlight()
        {
            var inputs = Inputs(
                camera: CameraState.GamePlay,
                pauseState: 9,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void Disconnected_EndsFlight()
        {
            // Regression: previously unreachable, because after a disconnect none of the
            // properties that triggered re-evaluation ever changed again.
            var inputs = Inputs(
                camera: CameraState.Cockpit,
                isConnected: false,
                wasInFlight: true);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void LoadedFlightNotMainMenu_IsInFlight()
        {
            var inputs = Inputs(camera: CameraState.GamePlay, pauseState: 0);

            Assert.True(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void MainMenuFlt_IsNotInFlight()
        {
            var inputs = Inputs(camera: CameraState.MenuRtc, loadedFlight: MainMenuFlt);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(8u)]
        [InlineData(9u)]
        public void PausedWithNoLiveCamera_IsNotInFlight(uint pauseState)
        {
            var inputs = Inputs(camera: CameraState.GamePlay, pauseState: pauseState);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }

        [Fact]
        public void EmptyLoadedFlight_IsNotInFlight()
        {
            var inputs = Inputs(camera: CameraState.MenuRtc, loadedFlight: string.Empty);

            Assert.False(FlightStateEvaluator.IsInFlight(inputs));
        }
    }
}
