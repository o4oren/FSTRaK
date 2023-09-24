using System;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class LandedState : AbstractState
    {
        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }
        public LandedState(FlightManager context, FlightData landingData) : base(context)
        {
            this.EventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

            ProcessLandingData(landingData);

        }

        private void ProcessLandingData(FlightData landingData)
        {
            var le = new LandingEvent()
            {
                VerticalSpeed = landingData.VerticalSpeed
            };

            if (landingData.VerticalSpeed < -500)
            {
                le.LandingRate = LandingRate.Hard;
                le.ScoreDelta = -35;
            }
            else if (landingData.VerticalSpeed < -350)
            {
                le.LandingRate = LandingRate.Fair;
                le.ScoreDelta = -10;
            }
            else if (landingData.VerticalSpeed < -190)
            {
                le.LandingRate = LandingRate.Good;
            }
            else if (landingData.VerticalSpeed < -165)
            {   
                le.LandingRate = LandingRate.Perfect;
                le.ScoreDelta = +10;
            }
            else if (landingData.VerticalSpeed < -135)
            {
                le.LandingRate = LandingRate.Good;
            }
            else if (landingData.VerticalSpeed < -101)
            {
                le.LandingRate = LandingRate.Soft;
                le.ScoreDelta = -10;
            }

            // TODO future handling of pitch
            Log.Information($"Parked! Flaps: {le.FlapsPosition}, with {le.FuelWeightLbs} Lbs of fuel.");


            AddFlightEvent(landingData, le);
        }

        public override void ProcessFlightData(FlightData data)
        {
            if (!Convert.ToBoolean(data.SimOnGround))
            {
                Context.State = new FlightState(Context);
                return;
            }

            if (data.GroundVelocity < 35 && data.MaxThrottlePosition() < 50)
            {
                var ti = new TaxiInEvent()
                {
                    FuelWeightLbs = data.FuelWeightLbs
                };
                AddFlightEvent(data, ti);
                Context.State = new TaxiInState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                AddFlightEvent(data, new BaseFlightEvent());
                Stopwatch.Restart();
            }
        }
    }
}
