using System;


namespace Devian
{
    [Serializable]
    public sealed class SaveCloudPayload
    {
        public int Version;
        public string UpdateTime;
        public string Payload;   // JSON string (recommended)
        public string DeviceId;
        public long SaveSeq;

        public SaveCloudPayload(int version, string updateTime, string payload, string deviceId = null, long saveSeq = 0L)
        {
            Version = version;
            UpdateTime = updateTime;
            Payload = payload;
            DeviceId = deviceId;
            SaveSeq = saveSeq;
        }
    }
}
