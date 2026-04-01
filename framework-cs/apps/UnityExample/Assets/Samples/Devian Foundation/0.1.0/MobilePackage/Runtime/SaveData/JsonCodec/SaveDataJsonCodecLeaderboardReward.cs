using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecLeaderboardReward
    {
        public static JObject Serialize(LeaderboardSeasonRewardStorage storage)
        {
            storage ??= new LeaderboardSeasonRewardStorage();

            var rewardObj = new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
            };

            var claimsObj = new JObject();
            foreach (var claim in storage.processedClaims)
            {
                if (string.IsNullOrWhiteSpace(claim.Key) || claim.Value == null)
                    continue;

                claimsObj[claim.Key] = new JObject
                {
                    ["resultType"] = (int)claim.Value.resultType,
                    ["rank"] = claim.Value.rank,
                    ["score"] = claim.Value.score,
                    ["reward_group_id"] = claim.Value.rewardGroupId ?? string.Empty,
                    ["evaluatedAtServerUtcMs"] = claim.Value.evaluatedAtServerUtcMs,
                };
            }

            rewardObj["processedClaims"] = claimsObj;
            return rewardObj;
        }

        public static void DeserializeInto(JObject rewardObj, LeaderboardSeasonRewardStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (rewardObj == null)
                return;

            storage.schemaVersion = rewardObj.Value<int?>("schemaVersion") ?? 1;
            if (rewardObj["processedClaims"] is not JObject claimsObj)
                return;

            foreach (var claimProp in claimsObj.Properties())
            {
                if (string.IsNullOrWhiteSpace(claimProp.Name) || claimProp.Value is not JObject claimObj)
                    continue;

                var resultTypeRaw = claimObj.Value<int?>("resultType") ?? 0;
                var resultType = System.Enum.IsDefined(typeof(LeaderboardSeasonRewardResultType), resultTypeRaw)
                    ? (LeaderboardSeasonRewardResultType)resultTypeRaw
                    : LeaderboardSeasonRewardResultType.NONE;

                storage.SetClaim(claimProp.Name, new LeaderboardSeasonRewardClaimRecord
                {
                    resultType = resultType,
                    rank = claimObj.Value<long?>("rank") ?? 0L,
                    score = claimObj.Value<long?>("score") ?? 0L,
                    rewardGroupId = claimObj.Value<string>("reward_group_id") ?? string.Empty,
                    evaluatedAtServerUtcMs = claimObj.Value<long?>("evaluatedAtServerUtcMs") ?? 0L,
                });
            }
        }
    }
}
