using System;

namespace Devian
{
    [Serializable]
    public enum MissionRuntimeState
    {
        NONE = 0,
        WAIT = 1,
        ACTIVE = 2,
        CLAIMABLE = 3,
        COMPLETED = 4,
    }
}
