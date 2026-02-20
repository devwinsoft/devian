using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class SaveSlotConfig
    {
        [Header("Slots (Shared)")]
        public List<SaveSlot> slots = new();

        public List<string> GetLocalSlotKeys()
        {
            var keys = new List<string>(slots?.Count ?? 0);
            if (slots == null) return keys;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                var key = s.slotKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var filename = s.filename?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(filename)) continue;

                if (!SaveDataManager.IsValidJsonFilename(filename, out _)) continue;

                keys.Add(key);
            }
            return keys;
        }

        public List<string> GetCloudSlotKeys()
        {
            var keys = new List<string>(slots?.Count ?? 0);
            if (slots == null) return keys;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                var key = s.slotKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var cloudSlot = s.cloudSlot?.Trim();
                if (string.IsNullOrWhiteSpace(cloudSlot)) continue;

                keys.Add(key);
            }
            return keys;
        }

        public bool TryResolveLocalFilename(string slotKey, out string filename)
        {
            filename = null;
            if (slots == null) return false;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                if (!string.Equals(s.slotKey, slotKey, StringComparison.Ordinal)) continue;

                var fn = s.filename?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(fn)) return false;

                filename = fn;
                return true;
            }
            return false;
        }

        public bool TryResolveCloudSlot(string slotKey, out string cloudSlot)
        {
            cloudSlot = null;
            if (slots == null) return false;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                if (!string.Equals(s.slotKey, slotKey, StringComparison.Ordinal)) continue;

                cloudSlot = s.cloudSlot;
                return !string.IsNullOrWhiteSpace(cloudSlot);
            }
            return false;
        }
    }
}
