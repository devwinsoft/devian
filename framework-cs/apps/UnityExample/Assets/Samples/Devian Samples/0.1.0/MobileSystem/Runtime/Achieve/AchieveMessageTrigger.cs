using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Achieve message callback payload convention:
    /// - RUNTIME_INIT: args[0] = AchieveRuntime
    /// - RUNTIME_ACTIVE: args[0] = AchieveRuntime
    /// - RUNTIME_PROGRESS: args[0] = AchieveRuntime
    /// - RUNTIME_CLAIMABLE: args[0] = AchieveRuntime
    /// - RUNTIME_LEVEL_UP: args[0] = AchieveRuntime
    /// - RUNTIME_REWARDED: args[0] = AchieveRuntime, args[1] = RewardData[]
    /// - RUNTIME_UNLOCKED: args[0] = string achievementId
    /// </summary>
    public sealed class AchieveMessageTrigger : BaseTrigger<EntityId, ACHIEVE_MESSAGE>
    {
        public void Notify(ACHIEVE_MESSAGE msgType, AchieveRuntime runtime)
        {
            base.Notify(msgType, runtime);
        }

        public void Notify(ACHIEVE_MESSAGE msgType, AchieveRuntime runtime, params object[] extras)
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

        public void Notify(ACHIEVE_MESSAGE msgType, string achievementId)
        {
            base.Notify(msgType, achievementId);
        }
    }
}
