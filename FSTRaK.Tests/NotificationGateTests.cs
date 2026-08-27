using System;
using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class NotificationGateTests
    {
        // A hand-cranked clock: the tests advance time explicitly rather than sleeping,
        // so they stay fast and deterministic.
        private sealed class FakeClock
        {
            public long NowMs;
            public Func<long> Read => () => NowMs;
        }

        [Fact]
        public void ShouldNotify_FirstSample_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SampleInsideTheInterval_IsSuppressed()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;

            Assert.False(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SampleAfterTheInterval_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1060;

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_ExactlyAtTheInterval_IsAdmitted()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1050;

            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_AdmittingResetsTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1060;
            Assert.True(gate.ShouldNotify(false));

            // 20ms past the sample that was just admitted, not 80ms past the first one.
            clock.NowMs = 1080;
            Assert.False(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_SuppressingDoesNotResetTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            // A burst of suppressed samples must not push the next admission out.
            clock.NowMs = 1010;
            Assert.False(gate.ShouldNotify(false));
            clock.NowMs = 1030;
            Assert.False(gate.ShouldNotify(false));

            clock.NowMs = 1050;
            Assert.True(gate.ShouldNotify(false));
        }

        [Fact]
        public void ShouldNotify_Forced_IsAdmittedInsideTheInterval()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;

            // The identity check after a reconnect must never be gated away.
            Assert.True(gate.ShouldNotify(true));
        }

        [Fact]
        public void ShouldNotify_Forced_ResetsTheWindow()
        {
            var clock = new FakeClock { NowMs = 1000 };
            var gate = new NotificationGate(50, clock.Read);
            gate.ShouldNotify(false);

            clock.NowMs = 1010;
            Assert.True(gate.ShouldNotify(true));

            clock.NowMs = 1040;
            Assert.False(gate.ShouldNotify(false));
        }
    }
}
