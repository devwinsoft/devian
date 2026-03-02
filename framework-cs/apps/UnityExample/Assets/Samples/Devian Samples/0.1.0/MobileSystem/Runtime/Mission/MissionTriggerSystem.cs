using Devian.Domain.Game;

namespace Devian
{
    public sealed class MissionTriggerSystem : MessageSystem<int, MISSION_CONDITION_TYPE>
    {
        public void Notify(MISSION_CONDITION_TYPE msgType, long msgValue)
        {
            base.Notify(msgType, CBigInt.FromLong(msgValue));
        }

        public void Notify(MISSION_CONDITION_TYPE msgType, int msgValue)
        {
            base.Notify(msgType, CBigInt.FromInt(msgValue));
        }

        public void Notify(MISSION_CONDITION_TYPE msgType, CBigInt msgValue)
        {
            base.Notify(msgType, msgValue);
        }
    }
}
