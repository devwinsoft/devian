using System;

namespace Devian
{
    [Serializable]
    public sealed class LocalSavePayload
    {
        public int version;
        public long updatedAtUtc;
        public string payload;
        public string checksum;

        public LocalSavePayload(int version, long updatedAtUtc, string payload, string checksum)
        {
            this.version = version;
            this.updatedAtUtc = updatedAtUtc;
            this.payload = payload;
            this.checksum = checksum;
        }
    }
}
