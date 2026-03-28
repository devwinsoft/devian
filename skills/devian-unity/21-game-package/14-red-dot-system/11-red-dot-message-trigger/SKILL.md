# 11-red-dot-message-trigger

Status: ACTIVE
AppliesTo: v10

## Overview

GamePackage red dot 변경 알림 라우터다.

- 타입: `BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>`
- ownerKey: subscriber `UnityEngine.EntityId`
- 실소유자는 `RedDotManager`다.

---

## Contract

```csharp
public sealed class RedDotMessageTrigger : BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>
{
    public void NotifyStateChanged(RedDotChanged changed);
}
```

규칙:
- TriggerSystem은 순수 구독 라우터다.
- 외부는 `RedDotManager.Subcribe(...)`, `UnSubcribe(...)` helper를 사용한다.
- trigger 인스턴스를 외부에 직접 노출하지 않는다.
- payload는 항상 `args[0] = RedDotChanged`다.

---

## Message Keys

`RED_DOT_MESSAGE_TYPE`

- `NONE`
- `STATE_CHANGED`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-red-dot-manager](../10-red-dot-manager/SKILL.md)
- [../../../20-common-package/25-trigger](../../../20-common-package/25-trigger/SKILL.md)
