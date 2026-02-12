using System;

namespace Devian
{
    [Serializable]
    public sealed class LocalSavePayload
    {
        public int version;
        public string updateTime;
        public long utcTime;
        public string payload;
        public string checksum;

        public LocalSavePayload(int version, string updateTime, long utcTime, string payload, string checksum)
        {
            this.version = version;
            this.updateTime = updateTime;
            this.utcTime = utcTime;
            this.payload = payload;
            this.checksum = checksum;
        }
    }
}
