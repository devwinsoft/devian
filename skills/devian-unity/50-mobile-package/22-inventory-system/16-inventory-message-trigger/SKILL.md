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

`INVENTORY_MESSAGE_TYPE` enum(입력: `input/Domains/Game/ENUM_INVENTORY.json`)을 사용한다.

키:

- `NONE`
- `PASS_OWNERSHIP_CHANGED`
- `CURRENCY_CHANGED`
- `ITEM_EQUIP_CHANGED`
- `ITEM_CARD_CHANGED`
- `ITEM_MATERIAL_CHANGED`
- `ITEM_HERO_CHANGED`
- `ITEM_EQUIP_LIST_CHANGED`
- `ITEM_CARD_LIST_CHANGED`
- `ITEM_MATERIAL_LIST_CHANGED`
- `ITEM_HERO_LIST_CHANGED`
- `RENTAL_CHANGED`
- `TREASURE_STATE_CHANGED`
- `INVENTORY_SNAPSHOT_CHANGED`

payload 규약:

- `PASS_OWNERSHIP_CHANGED`: `args[0] = string passId`, `args[1] = bool owned`
- `CURRENCY_CHANGED`: `args[0] = CURRENCY_TYPE`, `args[1] = long delta`, `args[2] = long currentAmount`
- `ITEM_EQUIP_CHANGED`: `args[0] = string itemUid`, `args[1] = string itemId`, `args[2] = AbilityItemEquip runtime`
- `ITEM_CARD_CHANGED`: `args[0] = string itemId`, `args[1] = AbilityItemCard runtime`
- `ITEM_MATERIAL_CHANGED`: `args[0] = string itemId`, `args[1] = AbilityItemMaterial runtime`
- `ITEM_HERO_CHANGED`: `args[0] = string itemId`, `args[1] = AbilityItemHero runtime`
- `ITEM_EQUIP_LIST_CHANGED`: `args[0] = INVENTORY_LIST_CHANGE_TYPE`, `args[1] = string itemUid`, `args[2] = string itemId`, `args[3] = AbilityItemEquip runtimeOrNull`
- `ITEM_CARD_LIST_CHANGED`: `args[0] = INVENTORY_LIST_CHANGE_TYPE`, `args[1] = string itemId`, `args[2] = AbilityItemCard runtimeOrNull`
- `ITEM_MATERIAL_LIST_CHANGED`: `args[0] = INVENTORY_LIST_CHANGE_TYPE`, `args[1] = string itemId`, `args[2] = AbilityItemMaterial runtimeOrNull`
- `ITEM_HERO_LIST_CHANGED`: `args[0] = INVENTORY_LIST_CHANGE_TYPE`, `args[1] = string itemId`, `args[2] = AbilityItemHero runtimeOrNull`
- `RENTAL_CHANGED`: `args[0] = string itemId`, `args[1] = long expiresAtClientUtcMs`, `args[2] = bool active`
- `TREASURE_STATE_CHANGED`: `args[0] = TREASURE_GRADE_TYPE`, `args[1] = int deltaCount`, `args[2] = int currentCount`, `args[3] = int currentLevel`, `args[4] = int currentExp`
- `INVENTORY_SNAPSHOT_CHANGED`: `args[0] = INVENTORY_SNAPSHOT_CHANGE_REASON`

---

## Usage Rules

- `AchieveManager`는 `ACHIEVE_PASS.req_pass_id` 조건 runtime의 WAIT 활성 전이를 위해 Inventory 메시지를 구독한다.
- `InventoryManager`는 item/currency 변경을 타입별 `*_CHANGED` key로 발행한다.
- item 목록 추가/삭제는 타입별 `*_LIST_CHANGED` key로 발행한다.
- bulk load/import/clear는 `INVENTORY_SNAPSHOT_CHANGED` 한 번으로 알린다.
- 직접 `InventoryMessageTrigger` 인스턴스를 참조/주입하지 않는다.

---

## Related

- [10-inventory-manager](../10-inventory-manager/SKILL.md)
- [11-inventory-storage](../11-inventory-storage/SKILL.md)
- [46-achieve-system/10-achieve-manager](../../46-achieve-system/10-achieve-manager/SKILL.md)
