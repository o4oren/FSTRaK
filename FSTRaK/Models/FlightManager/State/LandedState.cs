

using FSTRaK.DataTypes;
using System;
using System.Windows.Markup;

namespace FSTRaK.Models.FlightManager
{
    internal class LandedState : AbstractState
    {
        public override string Name { get; set; }
        public override bool IsMovementState { get; set; }
        public LandedState(FlightManager Context, AircraftFlightData LandingData) : base(Context)
        {
            this._eventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            Context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

            ProcessLandingData(LandingData);

        }

        private void ProcessLandingData(AircraftFlightData landingData)
        {
            LandingEvent le = new LandingEvent()
            {
                VerticalSpeed = landingData.VerticalSpeed
            };

            if (landingData.VerticalSpeed < -500)
            {
                le.LandingRate = LandingRate.Hard;
                le.ScoreDelta -= 20;
            }
            else if (landingData.VerticalSpeed < -350)
            {
                le.LandingRate = LandingRate.Aceeptable;
                le.ScoreDelta -= 10;
            }
            else if (landingData.VerticalSpeed < -275)
            {
                le.LandingRate = LandingRate.Good;
            }
            else if (landingData.VerticalSpeed < -175)
            {
                le.LandingRate = LandingRate.Perfect;
                le.ScoreDelta += 20;
            }
            else if (landingData.VerticalSpeed < -125)
            {
                le.LandingRate = LandingRate.Soft;
                le.ScoreDelta -= 10;
            }

            // TODO future handling of pitch


            AddFlightEvent(landingData, le);
        }

        public override void ProcessFlightData(AircraftFlightData Data)
        {
            if (!Convert.ToBoolean(Data.SimOnGround))
            {
                Context.State = new FlightState(Context);
                return;
            }

            if (Data.GroundVelocity < 35)
            {
                TaxiInEvent ti = new TaxiInEvent()
                {
                    FuelWeightLbs = Data.FuelWeightLbs
                };
                AddFlightEvent(Data, ti);
                Context.State = new TaxiInState(Context);
                return;
            }

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > _eventInterval)
            {
                AddFlightEvent(Data, new FlightEvent());
                _stopwatch.Restart();
            }
        }
    }
}
