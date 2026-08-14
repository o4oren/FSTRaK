using System;
using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class LandedState : AbstractState
    {
        // A new touchdown within this window after the previous one is treated as a bounce
        // belonging to the same landing sequence, not as a separate landing.
        internal const int BounceWindowMs = 5000;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private readonly TouchdownTracker _touchdownTracker;

        public LandedState(FlightManager context, FlightData landingData, TouchdownTracker touchdownTracker = null) : base(context)
        {
            this.EventInterval = 5000;
            this.Name = "Landed";
            this.IsMovementState = true;

            if (touchdownTracker != null)
            {
                // A bounce of an ongoing landing sequence - fold this touchdown into it.
                _touchdownTracker = touchdownTracker;
                _touchdownTracker.RegisterBounce(landingData, context);
            }
            else
            {
                context.RequestNearestAirports(DataTypes.NearestAirportRequestType.Arrival);
                _touchdownTracker = new TouchdownTracker(ProcessLandingData(landingData, context), landingData);
            }
        }

        private LandingEvent ProcessLandingData(FlightData landingData, FlightManager context)
        {
            var le = new LandingEvent()
            {
                VerticalSpeed = landingData.VerticalSpeed,
                TouchDownPitchDegrees = landingData.PitchDegrees,
                TouchDownBankDegress = landingData.BankDegrees
            };

            TouchdownTracker.ApplyVerticalSpeedRating(le);

            Log.Information($"Landed! Flaps: {le.FlapsPosition}, VS: {le.VerticalSpeed:F0} fpm, with {landingData.FuelWeightLbs} Lbs of fuel.");


            AddFlightEvent(landingData, le);
            context.ActiveFlight.LandingFpm = le.VerticalSpeed;

            return le;
        }

        public override void ProcessFlightData(FlightData data)
        {
            _touchdownTracker.Update(data);

            if (_touchdownTracker.MillisecondsSinceLastTouchdown >= BounceWindowMs)
            {
                _touchdownTracker.FinalizeLanding();
            }

            if (!Convert.ToBoolean(data.SimOnGround))
            {
                // While the landing sequence is still open, carry it into the flight state so a
                // quick re-touchdown (bounce) merges into the same landing event.
                Context.State = _touchdownTracker.IsFinalized
                    ? new FlightState(Context)
                    : new FlightState(Context, _touchdownTracker);
                return;
            }

            if (data.GroundVelocity < 35 && data.MaxThrottlePosition() < 50)
            {
                _touchdownTracker.FinalizeLanding();
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
            // Called on every data tick - only finalize when the sim actually left flight mode.
            if (!Context.SimConnectInFlight)
            {
                _touchdownTracker.FinalizeLanding();
            }
            base.HandleFlightExitEvent();
        }
    }
}
