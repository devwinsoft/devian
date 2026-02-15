using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class SaveLocalSlot
    {
        public string slotKey;
        public string filename;
    }

    public enum SaveLocalRoot
    {
        PersistentData,
        TemporaryCache
    }
}
