using FSTRaK.DataTypes;
using FSTRaK.Models;
using Serilog;

namespace FSTRaK.BusinessLogic.FlightManager.State
{
    /// <summary>
    /// Tracks a single landing sequence, possibly spanning several touchdowns (bounces).
    /// Keeps the worst vertical speed and the peak G force over all touchdowns, and on
    /// finalization applies the worst of the FPM-based and G-based score deltas.
    /// </summary>
    internal class TouchdownTracker
    {
        // G force is only sampled this long after each touchdown, so rollout braking is not counted.
        private const int GForceWindowMs = 2000;

        private readonly LandingEvent _landingEvent;
        private readonly System.Diagnostics.Stopwatch _touchdownStopwatch = new System.Diagnostics.Stopwatch();
        private double _maxGForce;
        private bool _finalized;

        public bool IsFinalized => _finalized;
        public long MillisecondsSinceLastTouchdown => _touchdownStopwatch.ElapsedMilliseconds;

        public TouchdownTracker(LandingEvent landingEvent, FlightData touchdownData)
        {
            _landingEvent = landingEvent;
            _maxGForce = touchdownData.GForce;
            _touchdownStopwatch.Start();
        }

        /// <summary>
        /// Registers another touchdown belonging to the same landing sequence (a bounce).
        /// The landing event keeps the worst vertical speed seen so far and the G sampling
        /// window restarts so the new impact is captured as well.
        /// </summary>
        public void RegisterBounce(FlightData touchdownData, FlightManager context)
        {
            if (_finalized)
            {
                return;
            }

            if (touchdownData.GForce > _maxGForce)
            {
                _maxGForce = touchdownData.GForce;
            }

            if (touchdownData.VerticalSpeed < _landingEvent.VerticalSpeed)
            {
                _landingEvent.VerticalSpeed = touchdownData.VerticalSpeed;
                ApplyVerticalSpeedRating(_landingEvent);
                context.ActiveFlight.LandingFpm = _landingEvent.VerticalSpeed;
            }

            Log.Information($"Bounced! Touched down again at {touchdownData.VerticalSpeed:F0} fpm, {touchdownData.GForce:F2} G.");
            _touchdownStopwatch.Restart();
        }

        /// <summary>
        /// Samples G force while within the post-touchdown window.
        /// </summary>
        public void Update(FlightData data)
        {
            if (_finalized || _touchdownStopwatch.ElapsedMilliseconds > GForceWindowMs)
            {
                return;
            }

            if (data.GForce > _maxGForce)
            {
                _maxGForce = data.GForce;
            }
        }

        /// <summary>
        /// Closes the landing sequence: stores the peak G on the landing event
        /// and applies the worst (lowest) of the FPM-based and G-based score deltas.
        /// The rating label follows whichever delta is worse; ties keep the FPM rating.
        /// </summary>
        public void FinalizeLanding()
        {
            if (_finalized)
            {
                return;
            }
            _finalized = true;

            _landingEvent.TouchdownGForce = _maxGForce;

            GetGForceRating(_maxGForce, out var gRating, out var gDelta);

            if (gDelta < _landingEvent.ScoreDelta)
            {
                _landingEvent.ScoreDelta = gDelta;
                _landingEvent.LandingRate = gRating;
            }

            Log.Information($"Touchdown G: {_maxGForce:F2}, landing scored as {_landingEvent.LandingRate} ({_landingEvent.ScoreDelta} points).");
        }

        /// <summary>
        /// Sets the FPM-based rating and score delta on the landing event from its vertical speed.
        /// </summary>
        public static void ApplyVerticalSpeedRating(LandingEvent le)
        {
            if (le.VerticalSpeed < -500)
            {
                le.LandingRate = LandingRate.Hard;
                le.ScoreDelta = -35;
            }
            else if (le.VerticalSpeed < -350)
            {
                le.LandingRate = LandingRate.Fair;
                le.ScoreDelta = -10;
            }
            else if (le.VerticalSpeed < -190)
            {
                le.LandingRate = LandingRate.Good;
                le.ScoreDelta = 0;
            }
            else if (le.VerticalSpeed < -165)
            {
                le.LandingRate = LandingRate.Perfect;
                le.ScoreDelta = +10;
            }
            else if (le.VerticalSpeed < -135)
            {
                le.LandingRate = LandingRate.Good;
                le.ScoreDelta = 0;
            }
            else
            {
                le.LandingRate = LandingRate.Soft;
                le.ScoreDelta = le.VerticalSpeed < -101 ? -10 : 0;
            }
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
