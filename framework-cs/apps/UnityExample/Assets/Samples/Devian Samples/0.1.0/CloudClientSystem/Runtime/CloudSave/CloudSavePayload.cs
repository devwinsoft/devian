using System;


namespace Devian
{
    [Serializable]
    public sealed class CloudSavePayload
    {
        public int Version;
        public string UpdateTime;
        public string Payload;   // JSON string (recommended)
        public string DeviceId;

        public CloudSavePayload(int version, string updateTime, string payload, string deviceId = null)
        {
            Version = version;
            UpdateTime = updateTime;
            Payload = payload;
            DeviceId = deviceId;
        }
    }
}
