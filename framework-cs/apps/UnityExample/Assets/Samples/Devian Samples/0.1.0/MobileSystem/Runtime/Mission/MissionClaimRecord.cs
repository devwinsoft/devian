using System;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public sealed class MissionClaimRecord
    {
        public MISSION_TYPE missionKind;
        public string missionId = string.Empty;
        public int level = 1;
        public string grantId = string.Empty;
        public string periodKey = string.Empty;
        public string rewardGroupId = string.Empty;
        public long claimedAtClientUtcMs;
    }
}
