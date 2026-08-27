using System;
using System.Diagnostics;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Rate-limits UI property-change notifications for flight data.
    ///
    /// The flight data subscription runs at SIM_FRAME, which follows the simulator's
    /// physics loop - commonly 60-120+Hz. Raising a property change at that rate floods
    /// the UI thread with binding invalidation and makes the application unresponsive to
    /// input, which is the defect this class exists to prevent.
    ///
    /// Deliberately gates only the notification, never the data. The state machine still
    /// sees every sample, because TouchdownTracker's peak G detection and the airborne
    /// SimOnGround transition both degrade if frames are dropped: a firm touchdown's G
    /// peak lasts only tens of milliseconds and a bounce can fall entirely between two
    /// gate windows.
    /// </summary>
    internal sealed class NotificationGate
    {
        private readonly long _intervalMs;
        private readonly Func<long> _clock;

        private long _lastNotifiedMs;
        private bool _hasNotified;

        /// <summary>
        /// Uses a monotonic wall clock. Stopwatch rather than DateTime so that a system
        /// clock adjustment mid-flight cannot stall notifications.
        /// </summary>
        public NotificationGate(long intervalMs)
            : this(intervalMs, CreateStopwatchClock())
        {
        }

        /// <summary>
        /// Test seam: the clock reads milliseconds from an arbitrary origin, so tests can
        /// advance time explicitly rather than sleeping.
        /// </summary>
        public NotificationGate(long intervalMs, Func<long> clock)
        {
            _intervalMs = intervalMs;
            _clock = clock;
        }

        private static Func<long> CreateStopwatchClock()
        {
            var stopwatch = Stopwatch.StartNew();
            return () => stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// True when the caller should raise a property change. Records the time on every
        /// admission - including a forced one - so a burst of suppressed samples never
        /// pushes the next admission out.
        /// </summary>
        /// <param name="force">
        /// Admits regardless of the interval. Set for the post-reconnect identity check,
        /// which reads LastKnownSnapshot and would compare against stale data if the
        /// notification that refreshes it were gated away.
        /// </param>
        public bool ShouldNotify(bool force)
        {
            var now = _clock();

            if (!force && _hasNotified && now - _lastNotifiedMs < _intervalMs)
            {
                return false;
            }

            _lastNotifiedMs = now;
            _hasNotified = true;
            return true;
        }
    }
}
