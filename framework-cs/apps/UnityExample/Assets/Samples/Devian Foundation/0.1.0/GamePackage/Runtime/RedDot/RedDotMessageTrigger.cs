using UnityEngine;

namespace Devian
{
    public sealed class RedDotMessageTrigger : BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>
    {
        public void NotifyStateChanged(RedDotChanged changed)
        {
            base.Notify(RED_DOT_MESSAGE_TYPE.STATE_CHANGED, changed);
        }
    }
}
