using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Mission message callback payload convention:
    /// - Default: args[0] = MissionRuntimeBase runtime
    /// - Extra payload: args[1+] = message-specific values
    /// - Exception: DAY_RESET is a global one-shot message and sends no args
    ///
    /// Message payloads:
    /// - RUNTIME_INIT: args[0] = MissionRuntimeBase
    /// - RUNTIME_PROGRESS: args[0] = MissionRuntimeBase
    /// - RUNTIME_CLAIMABLE: args[0] = MissionRuntimeBase
    /// - RUNTIME_REWARDED: args[0] = MissionRuntimeBase, args[1] = RewardData[]
    /// - ACHIEVE_LEVEL_UP: args[0] = MissionRuntimeBase
    /// - DAY_RESET: no args
    /// </summary>
    public sealed class MissionMessageTrigger : BaseTrigger<EntityId, MISSION_MESSAGE_TYPE>
    {
        public void Notify(MISSION_MESSAGE_TYPE msgType, MissionRuntimeBase runtime)
        {
            base.Notify(msgType, runtime);
        }

        public void Notify(MISSION_MESSAGE_TYPE msgType, MissionRuntimeBase runtime, params object[] extras)
        {
            if (extras == null || extras.Length == 0)
            {
                base.Notify(msgType, runtime);
                return;
            }

            var args = new object[extras.Length + 1];
            args[0] = runtime;
            for (var i = 0; i < extras.Length; i++)
                args[i + 1] = extras[i];

            base.Notify(msgType, args);
        }
    }
}
