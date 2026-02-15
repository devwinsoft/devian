using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class SaveSlot
    {
        public string slotKey;

        [Header("Local")]
        public string filename;

        [Header("Cloud")]
        public string cloudSlot;
    }
}
