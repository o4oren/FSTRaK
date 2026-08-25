using FSTRaK.BusinessLogic.SimconnectService;
using Xunit;

namespace FSTRaK.Tests
{
    public class ConnectionRecoveryTests
    {
        [Theory]
        [InlineData(0xC00000B0u)] // STATUS_PIPE_DISCONNECTED - the primary defect
        [InlineData(0xC000014Bu)] // STATUS_PIPE_BROKEN
        [InlineData(0x800706BAu)] // RPC_S_SERVER_UNAVAILABLE
        [InlineData(0x80004005u)] // E_FAIL
        [InlineData(0xDEADBEEFu)] // unrecognized
        public void ActionFor_AnyComError_Reconnects(uint hresult)
        {
            Assert.Equal(RecoveryAction.Reconnect, ConnectionRecovery.ActionFor(hresult));
        }

        [Fact]
        public void DescribeFor_KnownCodes_AreDistinct()
        {
            var pipeDisconnected = ConnectionRecovery.DescribeFor(0xC00000B0u);
            var pipeBroken = ConnectionRecovery.DescribeFor(0xC000014Bu);
            var rpcUnavailable = ConnectionRecovery.DescribeFor(0x800706BAu);

            Assert.NotEqual(pipeDisconnected, pipeBroken);
            Assert.NotEqual(pipeBroken, rpcUnavailable);
            Assert.NotEqual(pipeDisconnected, rpcUnavailable);
        }

        [Fact]
        public void DescribeFor_UnknownCode_MentionsTheCode()
        {
            var description = ConnectionRecovery.DescribeFor(0xDEADBEEFu);

            Assert.Contains("DEADBEEF", description, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
