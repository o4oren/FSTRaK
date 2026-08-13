using System;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class LandedState : AbstractState
    {
        private const int GForceWindowMs = 2000;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private readonly LandingEvent _landingEvent;
        private readonly System.Diagnostics.Stopwatch _touchdownStopwatch = new System.Diagnostics.Stopwatch();
        private double _maxGForce;
        private bool _gForceFinalized;

        public LandedState(FlightManager context, FlightData landingData) : base(context)
        {
            this.EventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;
            context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);

            _landingEvent = ProcessLandingData(landingData, context);
            _maxGForce = landingData.GForce;
            _touchdownStopwatch.Start();
        }

        private LandingEvent ProcessLandingData(FlightData landingData, FlightManager context)
        {
            var le = new LandingEvent()
            {
                VerticalSpeed = landingData.VerticalSpeed,
                TouchDownPitchDegrees = landingData.PitchDegrees,
                TouchDownBankDegress = landingData.BankDegrees
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

            Log.Information($"Landed! Flaps: {le.FlapsPosition}, VS: {le.VerticalSpeed:F0} fpm, with {landingData.FuelWeightLbs} Lbs of fuel.");


            AddFlightEvent(landingData, le);
            context.ActiveFlight.LandingFpm = le.VerticalSpeed;

            return le;
        }

        public override void ProcessFlightData(FlightData data)
        {
            if (!_gForceFinalized)
            {
                if (data.GForce > _maxGForce)
                {
                    _maxGForce = data.GForce;
                }

                if (_touchdownStopwatch.ElapsedMilliseconds >= GForceWindowMs)
                {
                    FinalizeTouchdownGForce();
                }
            }

            if (!Convert.ToBoolean(data.SimOnGround))
            {
                FinalizeTouchdownGForce();
                Context.State = new FlightState(Context);
                return;
            }

            if (data.GroundVelocity < 35 && data.MaxThrottlePosition() < 50)
            {
                FinalizeTouchdownGForce();
                AddFlightEvent(data, new TaxiInEvent());
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

        public override void HandleFlightExitEvent()
        {
            FinalizeTouchdownGForce();
            base.HandleFlightExitEvent();
        }

        /// <summary>
        /// Closes the post-touchdown max-G window: stores the peak G on the landing event
        /// and applies the worst (lowest) of the FPM-based and G-based score deltas.
        /// The rating label follows whichever delta is worse; ties keep the FPM rating.
        /// </summary>
        private void FinalizeTouchdownGForce()
        {
            if (_gForceFinalized)
            {
                return;
            }
            _gForceFinalized = true;

            _landingEvent.TouchdownGForce = _maxGForce;

            LandingRate gRating;
            int gDelta;
            GetGForceRating(_maxGForce, out gRating, out gDelta);

            if (gDelta < _landingEvent.ScoreDelta)
            {
                _landingEvent.ScoreDelta = gDelta;
                _landingEvent.LandingRate = gRating;
            }

            Log.Information($"Touchdown G: {_maxGForce:F2}, landing scored as {_landingEvent.LandingRate} ({_landingEvent.ScoreDelta} points).");
        }

        private static void GetGForceRating(double gForce, out LandingRate rating, out int scoreDelta)
        {
            if (gForce < 1.10)
            {
                rating = LandingRate.Soft;
                scoreDelta = -10;
            }
            else if (gForce < 1.15)
            {
                rating = LandingRate.Fair;
                scoreDelta = -10;
            }
            else if (gForce < 1.20)
            {
                rating = LandingRate.Good;
                scoreDelta = 0;
            }
            else if (gForce < 1.25)
            {
                rating = LandingRate.Perfect;
                scoreDelta = +10;
            }
            else if (gForce < 1.35)
            {
                rating = LandingRate.Good;
                scoreDelta = 0;
            }
            else if (gForce < 1.50)
            {
                rating = LandingRate.Fair;
                scoreDelta = -10;
            }
            else
            {
                rating = LandingRate.Hard;
                scoreDelta = -35;
            }
        }
    }
}
