using System;

namespace Devian
{
    [Serializable]
    public enum MissionRuntimeState
    {
        NONE = 0,
        ACTIVE = 1,
        CLAIMABLE = 2,
        COMPLETED = 3,
        WAIT = 4,
    }
}
