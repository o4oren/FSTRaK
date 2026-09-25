using System;
using System.Collections.Generic;
using FSTRaK.BusinessLogic.IvaoService.IvaoModel;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using FSTRaK.DataTypes;
using FSTRaK.ViewModels;
using Xunit;
using static FSTRaK.ViewModels.LiveViewViewModel;

namespace FSTRaK.Tests
{
    public class SelectedClientViewModelTests
    {
        private static SimTrafficAircraft BuildSimTrafficAircraft(int simOnGround)
        {
            var data = new SimTrafficData
            {
                Title = "Aircraft",
                AtcId = "TEST123",
                AtcType = "BOEING",
                AtcModel = "B738",
                Airline = "TestAir",
                FlightNumber = "TST001",
                Category = "Airplane",
                Latitude = 51.5,
                Longitude = -0.45,
                Altitude = 12345.6,
                TrueHeading = 271.4,
                GroundVelocity = 289.7,
                SimOnGround = simOnGround
            };

            return new SimTrafficAircraft(new SimTrafficEntry(42, data));
        }

        private static VatsimAicraft BuildVatsimAircraft()
        {
            var pilot = new Pilot
            {
                callsign = "DAL123",
                cid = 123456,
                altitude = 35000,
                groundspeed = 450,
                heading = 90,
                latitude = 40.0,
                longitude = -75.0,
                name = "Test Pilot"
            };

            return new VatsimAicraft(pilot);
        }

        private static IvaoAircraft BuildIvaoAircraft()
        {
            var pilot = new IvaoPilot
            {
                callsign = "KLM456",
                userId = 654321,
                lastTrack = new IvaoLastTrack
                {
                    latitude = 52.0,
                    longitude = 4.0,
                    altitude = 37000,
                    heading = 180,
                    groundSpeed = 470
                }
            };

            return new IvaoAircraft(pilot);
        }

        [Fact]
        public void SimTraffic_Selection_SetsExpectedFlags()
        {
            var aircraft = BuildSimTrafficAircraft(simOnGround: 0);

            var vm = new SelectedClientViewModel(aircraft);

            Assert.True(vm.IsSimTraffic);
            Assert.True(vm.IsPilot);
            Assert.False(vm.IsNetworkPilot);
            Assert.Equal(NetworkType.None, vm.Network);
        }

        [Fact]
        public void VatsimPilot_Selection_IsNetworkPilotAndNotSimTraffic()
        {
            var vatsimAircraft = BuildVatsimAircraft();

            var vm = new SelectedClientViewModel(vatsimAircraft, false, false, new List<TrackPoint>());

            Assert.True(vm.IsNetworkPilot);
            Assert.False(vm.IsSimTraffic);
        }

        [Fact]
        public void IvaoPilot_Selection_IsNetworkPilotAndNotSimTraffic()
        {
            var ivaoAircraft = BuildIvaoAircraft();

            var vm = new SelectedClientViewModel(ivaoAircraft, false, false, new List<TrackPoint>());

            Assert.True(vm.IsNetworkPilot);
            Assert.False(vm.IsSimTraffic);
        }

        [Fact]
        public void SimTraffic_PassesThroughAircraftFieldsToPanelBindings()
        {
            var aircraft = BuildSimTrafficAircraft(simOnGround: 0);

            var vm = new SelectedClientViewModel(aircraft);

            Assert.Equal(aircraft.Callsign, vm.Callsign);
            Assert.Equal(aircraft.Airline, vm.Airline);
            Assert.Equal(aircraft.AircraftType, vm.AircraftType);
            Assert.Equal(aircraft.Manufacturer, vm.Manufacturer);
            Assert.Equal(aircraft.Altitude, vm.Altitude);
            Assert.Equal(aircraft.Groundspeed, vm.Groundspeed);
            // The model's Heading is a double; the view model's is an int - verify the
            // rounding, not just an implicit truncating conversion.
            Assert.Equal((int)Math.Round(aircraft.Heading), vm.Heading);
        }

        [Fact]
        public void SimTraffic_OnGroundDisplay_ShowsAirborneWhenNotOnGround()
        {
            var aircraft = BuildSimTrafficAircraft(simOnGround: 0);

            var vm = new SelectedClientViewModel(aircraft);

            Assert.Equal("AIRBORNE", vm.OnGroundDisplay);
        }

        [Fact]
        public void SimTraffic_OnGroundDisplay_ShowsOnGroundWhenOnGround()
        {
            var aircraft = BuildSimTrafficAircraft(simOnGround: 1);

            var vm = new SelectedClientViewModel(aircraft);

            Assert.Equal("ON GROUND", vm.OnGroundDisplay);
        }
    }
}
