using FSTRaK.DataTypes;
using FSTRaK.Models;
using FSTRaK.Models.Entity.FlightEvent;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    internal class FlightState : AbstractState
    {
        // Ground contact within this time after liftoff is ignored - settling back onto the
        // gear right after rotation is not a landing.
        private const int TakeoffGraceMs = 5000;

        public sealed override string Name { get; set; }
        public sealed override bool IsMovementState { get; set; }

        private TouchdownTracker _pendingTouchdown;
        private readonly bool _justTookOff;
        private readonly System.Diagnostics.Stopwatch _airborneStopwatch = new System.Diagnostics.Stopwatch();

        public FlightState(FlightManager context, TouchdownTracker pendingTouchdown = null, bool justTookOff = false) : base(context)
        {
            this.EventInterval = 10000;
            this.Name = "In flight";
            this.IsMovementState = true;
            _pendingTouchdown = pendingTouchdown;
            _justTookOff = justTookOff;
            if (_pendingTouchdown != null || _justTookOff)
            {
                _airborneStopwatch.Start();
            }
        }
        public override void ProcessFlightData(FlightData data)
        {
            if (_pendingTouchdown != null && _airborneStopwatch.ElapsedMilliseconds >= LandedState.BounceWindowMs)
            {
                // Stayed airborne past the bounce window - a go-around; the landing sequence is over.
                _pendingTouchdown.FinalizeLanding();
                _pendingTouchdown = null;
            }

            if (data.SimOnGround == 1)
            {
                if (_pendingTouchdown != null)
                {
                    Context.State = new LandedState(Context, data, _pendingTouchdown);
                    return;
                }

                if (_justTookOff && _airborneStopwatch.ElapsedMilliseconds < TakeoffGraceMs)
                {
                    // Ignore a brief settle-back onto the runway right after liftoff.
                    return;
                }

                Context.State = new LandedState(Context, data);
                return;
            }

            if (data.IndicatedAirspeed < 150)
            {
                EventInterval = 6000;
            }
            else if (data.IndicatedAirspeed > 150 && data.IndicatedAirspeed < 250)
            {
                EventInterval = 10000;
            }
            else if (data.IndicatedAirspeed > 250 || data.Altitude > 10000)
            {
                EventInterval = 20000;
            }

            // TODO add code to handle specific flight events

            // Add event if stopwatch is not started, check if interval has elapsed otherwise
            if (!Stopwatch.IsRunning || Stopwatch.ElapsedMilliseconds > EventInterval)
            {
                var fe = CheckEnvelopeExceedingEvents(data);
                AddFlightEvent(data, fe);
                Stopwatch.Restart();
            }
        }

        public override void HandleFlightExitEvent()
        {
            // Called on every data tick - only finalize when the sim actually left flight mode.
            if (!Context.SimConnectInFlight)
            {
                _pendingTouchdown?.FinalizeLanding();
            }
            base.HandleFlightExitEvent();
        }

        private BaseFlightEvent CheckEnvelopeExceedingEvents(FlightData data)
        {
            if(data.OverSpeed == 1)
            {
                return new OverspeedEvent();
            }
            if (data.FlapSpeedExceeded == 1)
            {
                return new FlapsSpeedExceededEvent();
            }
            if (data.GearSpeedExceeded == 1)
            {
                return new GearsSpeedExceededEvent();
            }
            if (data.StallWarning == 1)
            {
                return new StallWarningEvent();
            }
            return new BaseFlightEvent();
        }
    }
}
