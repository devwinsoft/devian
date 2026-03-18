# 16-inventory-message-trigger

Status: ACTIVE
AppliesTo: v10

## Overview

Inventory 변동 알림 라우터다.

- 타입: `BaseTrigger<EntityId, INVENTORY_MESSAGE_TYPE>`
- ownerKey: subscriber-defined `EntityId`
- 실소유자는 `InventoryManager`다.

---

## Contract

```csharp
public sealed class InventoryMessageTrigger : BaseTrigger<EntityId, INVENTORY_MESSAGE_TYPE>
{
}
```

규칙:

- TriggerSystem은 순수 구독 라우터다.
- TriggerSystem 자체를 외부에 노출하지 않는다.
- 외부는 `InventoryManager` helper API로만 구독/해제한다.
- publish(`Notify`)는 `InventoryManager` 내부에서만 수행한다.
- TriggerSystem 자체는 저장/재생/큐잉 책임이 없다.

---

## Message Keys

`INVENTORY_MESSAGE_TYPE` enum(입력: `input/Domains/Game/ENUM_META.json`)을 사용한다.

최소 키:

- `NONE`
- `PASS_CHANGED`

payload 규약:

- `PASS_CHANGED`: `args[0] = string passId`, `args[1] = bool owned`

---

## Usage Rules

- `AchieveManager`는 `ACHIEVE_PASS.reqPassId` 조건 runtime의 WAIT 활성 전이를 위해 Inventory 메시지를 구독한다.
- `InventoryManager.Instance.Storage.Passes` 변동 시 `PASS_CHANGED`를 발행한다.
- 직접 `InventoryMessageTrigger` 인스턴스를 참조/주입하지 않는다.

---

## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md)
- [11-inventory-storage](../11-inventory-storage/SKILL.md)
- [46-achieve-system/10-achieve-manager](../../46-achieve-system/10-achieve-manager/SKILL.md)
