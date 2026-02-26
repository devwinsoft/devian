using System;

namespace Devian
{
    [Serializable]
    public sealed class SaveLocalPayload
    {
        public int version;
        public string updateTime;
        public string payload;
        public string deviceId;
        public long saveSeq;
        public AccountStorage account;

        public SaveLocalPayload(int version, string updateTime, string payload, string deviceId, long saveSeq = 0L, AccountStorage account = null)
        {
            this.version = version;
            this.updateTime = updateTime;
            this.payload = payload;
            this.deviceId = deviceId;
            this.saveSeq = saveSeq;
            this.account = account;
        }
    }
}
