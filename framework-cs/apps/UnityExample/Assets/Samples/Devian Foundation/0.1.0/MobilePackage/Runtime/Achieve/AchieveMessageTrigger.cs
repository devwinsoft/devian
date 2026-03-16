using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Achieve message callback payload convention:
    /// - RUNTIME_INIT: args[0] = AchieveRuntimeBase
    /// - RUNTIME_ACTIVE: args[0] = AchieveRuntimeBase
    /// - RUNTIME_PROGRESS: args[0] = AchieveRuntimeBase
    /// - RUNTIME_CLAIMABLE: args[0] = AchieveRuntimeBase
    /// - RUNTIME_LEVEL_UP: args[0] = AchieveRuntimeBase
    /// - RUNTIME_REWARDED: args[0] = AchieveRuntimeBase, args[1] = RewardData[]
    /// - RUNTIME_UNLOCKED: args[0] = string achievementId
    /// </summary>
    public sealed class AchieveMessageTrigger : BaseTrigger<EntityId, MESSAGE_ACHIEVE_TYPE>
    {
        public void Notify(MESSAGE_ACHIEVE_TYPE msgType, AchieveRuntimeBase runtime)
        {
            base.Notify(msgType, runtime);
        }

        public void Notify(MESSAGE_ACHIEVE_TYPE msgType, AchieveRuntimeBase runtime, params object[] extras)
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

        public void Notify(MESSAGE_ACHIEVE_TYPE msgType, string achievementId)
        {
            base.Notify(msgType, achievementId);
        }
    }
}
