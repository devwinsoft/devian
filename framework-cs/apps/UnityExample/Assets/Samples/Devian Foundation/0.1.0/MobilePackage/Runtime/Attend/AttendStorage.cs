using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class AttendStorage
    {
        public int schemaVersion = 2;
        public long cycleStartUtcMs;
        public long lastClaimUtcMs;
        public long lastLoginUtcMs;
        public int nextAttendDay = 1;
        public Dictionary<string, long> claimedAttendUtcMs = new();

        public bool IsClaimed(string attendId)
        {
            if (string.IsNullOrWhiteSpace(attendId))
                return false;

            return claimedAttendUtcMs.ContainsKey(attendId.Trim());
        }

        public bool TryGetClaimedAtUtcMs(string attendId, out long claimedAtUtcMs)
        {
            claimedAtUtcMs = 0L;
            if (string.IsNullOrWhiteSpace(attendId))
                return false;

            return claimedAttendUtcMs.TryGetValue(attendId.Trim(), out claimedAtUtcMs);
        }

        public void SetClaimed(string attendId, long claimedAtUtcMs)
        {
            if (string.IsNullOrWhiteSpace(attendId))
                return;

            var normalizedClaimedAtUtcMs = claimedAtUtcMs > 0L ? claimedAtUtcMs : 0L;
            claimedAttendUtcMs[attendId.Trim()] = normalizedClaimedAtUtcMs;
            lastClaimUtcMs = normalizedClaimedAtUtcMs;
        }

        public void MarkLogin(long loginUtcMs)
        {
            lastLoginUtcMs = loginUtcMs > 0L ? loginUtcMs : 0L;
        }

        public void ResetCycle(long cycleStartUtcMsValue)
        {
            cycleStartUtcMs = cycleStartUtcMsValue > 0L ? cycleStartUtcMsValue : 0L;
            nextAttendDay = 1;
            lastClaimUtcMs = 0L;
            claimedAttendUtcMs.Clear();
        }

        public void Clear()
        {
            schemaVersion = 2;
            cycleStartUtcMs = 0L;
            lastClaimUtcMs = 0L;
            lastLoginUtcMs = 0L;
            nextAttendDay = 1;
            claimedAttendUtcMs.Clear();
        }
    }
}
