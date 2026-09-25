using System;
using System.Collections.Generic;
using FSTRaK.BusinessLogic.SimconnectService;
using FSTRaK.DataTypes;
using Xunit;

namespace FSTRaK.Tests
{
    public class SimTrafficTrackerTests
    {
        private const uint AircraftRequest = (uint)Requests.SimTrafficAircraftRequest;
        private const uint HelicopterRequest = (uint)Requests.SimTrafficHelicopterRequest;

        // Long enough that the internal timer never fires during a test; every cycle is
        // driven explicitly by Poll().
        private const double NeverFires = 600000;

        private const uint UserObjectId = 1;

        private static SimTrafficData Data(string atcId)
        {
            return new SimTrafficData
            {
                Title = "Airbus A320",
                AtcId = atcId,
                AtcType = "A320",
                Airline = "",
                FlightNumber = "",
                Category = "Airplane",
                Latitude = 51.0,
                Longitude = -0.5,
                Altitude = 10000,
                TrueHeading = 90,
                GroundVelocity = 320,
                SimOnGround = 0
            };
        }

        private static SimTrafficTracker CreateTracker(out List<IReadOnlyList<SimTrafficEntry>> published)
        {
            var captured = new List<IReadOnlyList<SimTrafficEntry>>();
            var tracker = new SimTrafficTracker(() => { }, NeverFires);
            tracker.TrafficUpdated += snapshot => captured.Add(snapshot);
            tracker.SetUserObjectId(UserObjectId);
            published = captured;
            return tracker;
        }

        [Fact]
        public void Accept_CompleteBatch_PublishesOnceWithEveryEntry()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 2, Data("AAL1"));
            tracker.Accept(AircraftRequest, 11, 2, 2, Data("AAL2"));

            Assert.Single(published);
            Assert.Equal(2, published[0].Count);
        }

        [Fact]
        public void Accept_PartialBatch_PublishesNothing()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 3, Data("AAL1"));
            tracker.Accept(AircraftRequest, 11, 2, 3, Data("AAL2"));

            Assert.Empty(published);
        }

        [Fact]
        public void Accept_UserObjectId_IsExcludedFromTheSnapshot()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, UserObjectId, 1, 2, Data("MINE"));
            tracker.Accept(AircraftRequest, 11, 2, 2, Data("AAL2"));

            Assert.Single(published);
            Assert.Single(published[0]);
            Assert.Equal((uint)11, published[0][0].ObjectId);
        }

        [Fact]
        public void Accept_BeforeUserObjectIdIsKnown_PublishesNothing()
        {
            var published = new List<IReadOnlyList<SimTrafficEntry>>();
            var tracker = new SimTrafficTracker(() => { }, NeverFires);
            tracker.TrafficUpdated += snapshot => published.Add(snapshot);

            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));

            Assert.Empty(published);
        }

        [Fact]
        public void Accept_TornBatch_IsDiscardedRatherThanMerged()
        {
            var tracker = CreateTracker(out var published);

            // A batch that never completes, then a new cycle begins.
            tracker.Accept(AircraftRequest, 10, 1, 5, Data("STALE"));
            tracker.Poll();
            tracker.Accept(AircraftRequest, 20, 1, 1, Data("FRESH"));

            Assert.Single(published);
            Assert.Single(published[0]);
            Assert.Equal((uint)20, published[0][0].ObjectId);
        }

        [Fact]
        public void Accept_AircraftAndHelicopterBatches_MergeIntoOneSnapshot()
        {
            var tracker = CreateTracker(out var published);

            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            tracker.Accept(HelicopterRequest, 20, 1, 1, Data("HELI1"));

            Assert.Equal(2, published.Count);
            Assert.Equal(2, published[1].Count);
        }

        [Fact]
        public void Poll_TwoSilentCycles_PublishesAnEmptySnapshot()
        {
            var tracker = CreateTracker(out var published);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            published.Clear();

            tracker.Poll();
            Assert.Empty(published);

            tracker.Poll();

            Assert.Single(published);
            Assert.Empty(published[0]);
        }

        [Fact]
        public void Poll_ACompletedBatchResetsTheSilenceCounter()
        {
            var tracker = CreateTracker(out var published);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            tracker.Poll();

            // The second completed batch resets the counter, so the poll that follows is the
            // FIRST silent cycle, not the second, and must not clear the map.
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            published.Clear();

            tracker.Poll();

            Assert.Empty(published);
        }

        [Fact]
        public void UpdateRunState_RunsOnlyWhenEnabledConnectedAndStarted()
        {
            var tracker = CreateTracker(out _);

            tracker.UpdateRunState(false, true, true);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, false, true);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, true, false);
            Assert.False(tracker.IsRunning);

            tracker.UpdateRunState(true, true, true);
            Assert.True(tracker.IsRunning);

            tracker.UpdateRunState(false, true, true);
            Assert.False(tracker.IsRunning);
        }

        [Fact]
        public void UpdateRunState_StoppingClearsTheSnapshot()
        {
            var tracker = CreateTracker(out var published);
            tracker.UpdateRunState(true, true, true);
            tracker.Accept(AircraftRequest, 10, 1, 1, Data("AAL1"));
            published.Clear();

            tracker.UpdateRunState(false, true, true);

            Assert.Single(published);
            Assert.Empty(published[0]);
        }

        [Fact]
        public void Poll_InvokesTheRequestCallback()
        {
            var calls = 0;
            var tracker = new SimTrafficTracker(() => calls++, NeverFires);

            tracker.Poll();

            Assert.Equal(1, calls);
        }
    }
}
