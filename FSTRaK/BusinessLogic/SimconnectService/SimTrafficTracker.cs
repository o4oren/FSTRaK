using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// Keeps the current set of simulator traffic objects.
    ///
    /// RequestDataOnSimObjectType is a one-shot request - there is no PERIOD to attach - so
    /// currency comes from a timer rather than a subscription. Replies arrive one message
    /// per object, each carrying its position in the batch, and a snapshot is published only
    /// when a batch completes: the map must never render half a cycle.
    ///
    /// Holds no SimConnect handle deliberately. Issuing requests is the caller's job, passed
    /// in as onPoll, which is what makes this class testable in isolation - the same shape as
    /// <see cref="NotificationGate"/>.
    ///
    /// Not thread-safe. Like the rest of the SimConnect receive path it is called only from
    /// the WPF UI thread, except for the timer's Elapsed, which is marshalled by the caller.
    /// </summary>
    internal sealed class SimTrafficTracker
    {
        /// <summary>
        /// Two seconds. Fast enough that traffic does not visibly teleport, slow enough that
        /// a few hundred objects per cycle cost nothing measurable.
        /// </summary>
        public const double DefaultPollIntervalMs = 2000;

        /// <summary>80 nautical miles, in metres - SimConnect's radius unit.</summary>
        public const uint RadiusMeters = 148160;

        /// <summary>
        /// Consecutive silent cycles before a request's objects are cleared. A cycle that
        /// finds nothing produces no messages at all, so silence is the only signal that the
        /// traffic is gone; two cycles keeps a single dropped batch from blinking the map.
        /// </summary>
        private const int SilentCyclesBeforeClearing = 2;

        private static readonly uint[] TrackedRequests =
        {
            (uint)Requests.SimTrafficAircraftRequest,
            (uint)Requests.SimTrafficHelicopterRequest
        };

        private readonly Action _onPoll;
        private readonly Timer _pollTimer;

        // Committed objects per request type, replaced wholesale when a batch completes.
        private readonly Dictionary<uint, List<SimTrafficEntry>> _portions =
            new Dictionary<uint, List<SimTrafficEntry>>();

        // Batches still arriving, keyed by request type.
        private readonly Dictionary<uint, List<SimTrafficEntry>> _pending =
            new Dictionary<uint, List<SimTrafficEntry>>();

        private readonly Dictionary<uint, int> _silentCycles = new Dictionary<uint, int>();

        private uint? _userObjectId;

        public event Action<IReadOnlyList<SimTrafficEntry>> TrafficUpdated;

        public bool IsRunning { get; private set; }

        public SimTrafficTracker(Action onPoll)
            : this(onPoll, DefaultPollIntervalMs)
        {
        }

        public SimTrafficTracker(Action onPoll, double pollIntervalMs)
        {
            _onPoll = onPoll;

            foreach (var requestId in TrackedRequests)
            {
                _portions[requestId] = new List<SimTrafficEntry>();
                _silentCycles[requestId] = 0;
            }

            _pollTimer = new Timer(pollIntervalMs) { AutoReset = true };
            _pollTimer.Elapsed += (sender, e) => Poll();
        }

        /// <summary>
        /// The object ID the simulator uses for the user's own aircraft, taken from the
        /// flight data replies. By-type results include the user, and this is how that entry
        /// is recognised - reliably, rather than by assuming an ID or matching on position.
        /// </summary>
        public void SetUserObjectId(uint objectId)
        {
            _userObjectId = objectId;
        }

        /// <summary>
        /// Polling runs only while the layer is on, the connection is up, and the simulator
        /// is running. Switching off stops the timer and clears the map: a toggle that is off
        /// must cost nothing and leave nothing behind.
        /// </summary>
        public void UpdateRunState(bool layerEnabled, bool connected, bool simStarted)
        {
            var shouldRun = layerEnabled && connected && simStarted;

            if (shouldRun == IsRunning)
            {
                return;
            }

            IsRunning = shouldRun;

            if (shouldRun)
            {
                _pollTimer.Start();
            }
            else
            {
                _pollTimer.Stop();
                Reset();
            }
        }

        /// <summary>
        /// Opens a cycle: ages the silence counters, discards any batch left incomplete by
        /// the previous cycle, then asks the caller to issue the requests.
        /// </summary>
        public void Poll()
        {
            var changed = false;

            foreach (var requestId in TrackedRequests)
            {
                // A batch still pending when the next cycle opens lost messages somewhere.
                // Discard it - a snapshot must never mix two cycles.
                _pending.Remove(requestId);

                _silentCycles[requestId] = _silentCycles[requestId] + 1;

                if (_silentCycles[requestId] >= SilentCyclesBeforeClearing && _portions[requestId].Count > 0)
                {
                    _portions[requestId] = new List<SimTrafficEntry>();
                    changed = true;
                }
            }

            if (changed)
            {
                Publish();
            }

            _onPoll?.Invoke();
        }

        /// <summary>
        /// Folds one by-type reply into the batch it belongs to, publishing when the batch
        /// completes. Entry numbers are 1-based.
        /// </summary>
        public void Accept(uint requestId, uint objectId, uint entryNumber, uint outOf, SimTrafficData data)
        {
            if (!_portions.ContainsKey(requestId) || outOf == 0)
            {
                return;
            }

            // Until the user's own object ID is known, any snapshot risks drawing a duplicate
            // of the user's aircraft. Publishing nothing is the safer failure.
            if (_userObjectId == null)
            {
                return;
            }

            if (entryNumber == 1)
            {
                _pending[requestId] = new List<SimTrafficEntry>();
            }

            if (!_pending.TryGetValue(requestId, out var batch))
            {
                // Mid-batch arrival with no start - the head of this batch was lost.
                return;
            }

            if (objectId != _userObjectId.Value)
            {
                batch.Add(new SimTrafficEntry(objectId, data));
            }

            if (entryNumber >= outOf)
            {
                _portions[requestId] = batch;
                _pending.Remove(requestId);
                _silentCycles[requestId] = 0;
                Publish();
            }
        }

        /// <summary>
        /// Drops everything and publishes an empty snapshot. Used when the layer is switched
        /// off and when the connection dies, so no traffic is left frozen on the map.
        /// </summary>
        public void Reset()
        {
            _pending.Clear();

            foreach (var requestId in TrackedRequests)
            {
                _portions[requestId] = new List<SimTrafficEntry>();
                _silentCycles[requestId] = 0;
            }

            Publish();
        }

        private void Publish()
        {
            var merged = TrackedRequests
                .SelectMany(requestId => _portions[requestId])
                .ToList();

            TrafficUpdated?.Invoke(merged);
        }
    }
}
