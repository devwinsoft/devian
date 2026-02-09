using System;


namespace Devian
{
    [Serializable]
    public sealed class CloudSavePayload
    {
        public int Version;
        public long UpdatedAtUtc;
        public string Payload;   // JSON string (recommended)
        public string Checksum;  // optional (may be null/empty)

        public CloudSavePayload(int version, long updatedAtUtc, string payload, string checksum = null)
        {
            Version = version;
            UpdatedAtUtc = updatedAtUtc;
            Payload = payload;
            Checksum = checksum;
        }
    }
}
