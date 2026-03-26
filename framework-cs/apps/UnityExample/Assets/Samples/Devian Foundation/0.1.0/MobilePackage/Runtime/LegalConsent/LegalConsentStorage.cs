using System;

namespace Devian
{
    [Serializable]
    public sealed class LegalConsentStorage
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public bool isAccepted;
        public VersionNumber acceptedVersion;
        public long acceptedAtUtcMs;

        public void EnsureInitialized()
        {
            if (schemaVersion < CurrentSchemaVersion)
            {
                Clear();
            }
            else if (schemaVersion <= 0)
            {
                schemaVersion = CurrentSchemaVersion;
            }
        }

        public void SetAccepted(VersionNumber version, long acceptedAtUtcMsValue)
        {
            EnsureInitialized();

            isAccepted = true;
            acceptedVersion = version;
            acceptedAtUtcMs = acceptedAtUtcMsValue > 0L ? acceptedAtUtcMsValue : 0L;
        }

        public void Clear()
        {
            schemaVersion = CurrentSchemaVersion;
            isAccepted = false;
            acceptedVersion = default;
            acceptedAtUtcMs = 0L;
        }
    }
}
