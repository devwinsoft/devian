using UnityEngine;

namespace Devian
{
    public enum UI_MESSAGE
    {
        None,
        InitOnce,
        ReloadText,
        Resize,
    }

    public class UIMessageSystem : MessageSystem<EntityId, UI_MESSAGE>
    {
    }
}
