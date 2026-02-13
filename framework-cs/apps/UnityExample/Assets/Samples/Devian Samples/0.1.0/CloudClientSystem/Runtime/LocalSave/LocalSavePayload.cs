using System;

namespace Devian
{
    [Serializable]
    public sealed class LocalSavePayload
    {
        public int version;
        public string updateTime;
        public string payload;
        public string deviceId;

        public LocalSavePayload(int version, string updateTime, string payload, string deviceId)
        {
            this.version = version;
            this.updateTime = updateTime;
            this.payload = payload;
            this.deviceId = deviceId;
        }
    }
}
