using FSTRaK.DataTypes;

namespace FSTRaK.BusinessLogic.SimconnectService
{
    /// <summary>
    /// One traffic object paired with the SimConnect object ID it arrived under. The ID is
    /// what identifies an object across polls, and what the user-aircraft exclusion keys on.
    /// </summary>
    internal sealed class SimTrafficEntry
    {
        public uint ObjectId { get; }
        public SimTrafficData Data { get; }

        public SimTrafficEntry(uint objectId, SimTrafficData data)
        {
            ObjectId = objectId;
            Data = data;
        }
    }
}
