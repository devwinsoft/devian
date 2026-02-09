using System;


namespace Devian
{
    [Serializable]
    public sealed class CloudSavePayload
    {
        public int Version;
        public string UpdateTime;
        public long UtcTime;
        public string Payload;   // JSON string (recommended)
        public string Checksum;  // optional (may be null/empty)

        public CloudSavePayload(int version, string updateTime, long utcTime, string payload, string checksum = null)
        {
            Version = version;
            UpdateTime = updateTime;
            UtcTime = utcTime;
            Payload = payload;
            Checksum = checksum;
        }
    }
}
