using System;
using System.Collections.Generic;

namespace Devian
{
    public enum LeaderboardSeasonRewardResultType
    {
        NONE = 0,
        CLAIMED = 1,
        NO_PARTICIPATION = 2,
        RANK_OUT_OF_REWARD = 3,
    }

    [Serializable]
    public sealed class LeaderboardSeasonRewardClaimRecord
    {
        public LeaderboardSeasonRewardResultType resultType = LeaderboardSeasonRewardResultType.NONE;
        public long rank;
        public long score;
        public string rewardGroupId = string.Empty;
        public long evaluatedAtServerUtcMs;
    }

    [Serializable]
    public sealed class LeaderboardSeasonRewardStorage
    {
        public int schemaVersion = 1;
        public Dictionary<string, LeaderboardSeasonRewardClaimRecord> processedClaims = new();

        public void Clear()
        {
            schemaVersion = 1;
            processedClaims.Clear();
        }

        public bool TryGetClaim(string claimKey, out LeaderboardSeasonRewardClaimRecord claim)
        {
            claim = null;
            if (string.IsNullOrWhiteSpace(claimKey))
                return false;

            return processedClaims.TryGetValue(claimKey.Trim(), out claim) && claim != null;
        }

        public void SetClaim(string claimKey, LeaderboardSeasonRewardClaimRecord claim)
        {
            if (string.IsNullOrWhiteSpace(claimKey) || claim == null)
                return;

            processedClaims[claimKey.Trim()] = claim;
        }
    }
}
