namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// What the service should do when a COMException surfaces from a SimConnect call.
    /// </summary>
    internal enum RecoveryAction
    {
        /// <summary>Tear down the handle and start reconnecting.</summary>
        Reconnect,

        /// <summary>Record the error only; the connection remains usable.</summary>
        LogOnly
    }

    /// <summary>
    /// Maps COM error codes from SimConnect calls onto a recovery action.
    ///
    /// Every COMException reconnects. An error that leaves the pipe usable is the rare
    /// case, and treating an unknown code as recoverable is what produced the zombie
    /// state this class exists to prevent: the old handler recognised only
    /// STATUS_PIPE_BROKEN, so a STATUS_PIPE_DISCONNECTED left the data timer polling a
    /// dead pipe indefinitely. Failing closed costs a reconnect; failing open costs the
    /// flight.
    /// </summary>
    internal static class ConnectionRecovery
    {
        private const uint StatusPipeDisconnected = 0xC00000B0;
        private const uint StatusPipeBroken = 0xC000014B;
        private const uint RpcServerUnavailable = 0x800706BA;
        private const uint EFail = 0x80004005;

        public static RecoveryAction ActionFor(uint hresult)
        {
            return RecoveryAction.Reconnect;
        }

        public static string DescribeFor(uint hresult)
        {
            switch (hresult)
            {
                case StatusPipeDisconnected:
                    return "The simulator closed the SimConnect pipe (STATUS_PIPE_DISCONNECTED).";
                case StatusPipeBroken:
                    return "The SimConnect pipe is broken (STATUS_PIPE_BROKEN).";
                case RpcServerUnavailable:
                    return "The RPC server is unavailable (RPC_S_SERVER_UNAVAILABLE).";
                case EFail:
                    return "SimConnect reported an unspecified failure (E_FAIL).";
                default:
                    return $"Unrecognised SimConnect COM error 0x{hresult:X8}.";
            }
        }
    }
}
