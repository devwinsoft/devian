using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecAttend
    {
        public static JObject Serialize(AttendStorage storage)
        {
            storage ??= new AttendStorage();

            var attendObj = new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
                ["cycleStartUtcMs"] = storage.cycleStartUtcMs,
                ["lastClaimUtcMs"] = storage.lastClaimUtcMs,
                ["lastLoginUtcMs"] = storage.lastLoginUtcMs,
                ["nextAttendDay"] = storage.nextAttendDay,
            };

            var claimedObj = new JObject();
            foreach (var kv in storage.claimedAttendUtcMs)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                claimedObj[kv.Key] = kv.Value > 0L ? kv.Value : 0L;
            }

            attendObj["claimedAttendUtcMs"] = claimedObj;
            return attendObj;
        }

        public static void DeserializeInto(JObject attendObj, AttendStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();
            if (attendObj == null)
                return;

            storage.schemaVersion = attendObj.Value<int?>("schemaVersion") ?? 1;
            storage.cycleStartUtcMs = attendObj.Value<long?>("cycleStartUtcMs") ?? 0L;
            storage.lastClaimUtcMs = attendObj.Value<long?>("lastClaimUtcMs") ?? 0L;
            storage.lastLoginUtcMs = attendObj.Value<long?>("lastLoginUtcMs") ?? 0L;

            if (storage.cycleStartUtcMs < 0L)
                storage.cycleStartUtcMs = 0L;

            if (storage.lastClaimUtcMs < 0L)
                storage.lastClaimUtcMs = 0L;

            if (storage.lastLoginUtcMs < 0L)
                storage.lastLoginUtcMs = 0L;

            if (attendObj["claimedAttendUtcMs"] is JObject claimedObj)
            {
                foreach (var claimProp in claimedObj.Properties())
                {
                    if (string.IsNullOrWhiteSpace(claimProp.Name))
                        continue;

                    var claimedAtUtcMs = claimProp.Value.Value<long?>() ?? 0L;
                    if (claimedAtUtcMs < 0L)
                        claimedAtUtcMs = 0L;

                    storage.claimedAttendUtcMs[claimProp.Name.Trim()] = claimedAtUtcMs;
                }
            }

            storage.nextAttendDay = attendObj.Value<int?>("nextAttendDay") ?? 1;
            storage.nextAttendDay = clampNextAttendDay(storage.nextAttendDay);

            if (storage.schemaVersion < 2)
            {
                storage.schemaVersion = 2;
            }
        }

        static int clampNextAttendDay(int day)
        {
            if (day <= 1)
                return 1;

            if (day >= 8)
                return 8;

            return day;
        }
    }
}
