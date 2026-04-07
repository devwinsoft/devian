# 16-equip-slot-policy

Status: ACTIVE
AppliesTo: v10

`ITEM_EQUIP.equip_type`와 `GameConfigTable.xlsx.EQUIP_SLOT`를 연결해 outgame/preview 장비 슬롯 규칙을 정의한다.

---

## 1. SSOT

- enum 정본: `input/Domains/Game/ENUM_GAME.json`
  - `SLOT_TYPE`
  - `SLOT_TYPE.NONE = 0`
- 테이블 정본: `input/Domains/Game/GameConfigTable.xlsx`
  - `EQUIP_SLOT`
  - columns: `equip_type`, `allowed_slots`, `two_handed`
- 장비 row 정본: `input/Domains/Game/ItemTable.xlsx`
  - `ITEM_EQUIP.equip_type`

Generated output은 `Game.g.cs`, `Game.g.ts`, `EQUIP_SLOT.json`, `ITEM_EQUIP.json`으로 동기화된다.

---

## 2. Runtime Surface

- `AbilityEquipSlotPolicy.cs` / `AbilityEquipSlotPolicy.ts`
  - `GetRule(equipType)`
  - `IsAllowed(equip, slotType)`
  - `IsTwoHanded(rule|equip)`
  - `HasBlockingTwoHandedMain(equips)`
- `AbilityItemEquip`
  - `EquipType`
  - `OwnerSlotType`
- `AbilityItemHero`
  - `Dictionary<SLOT_TYPE, AbilityItemEquip>`
  - internal `_SetEquip(equip, slotType)`
  - internal `_RemoveEquip(slotType)`
- `AbilityUnitHero`
  - same slot key / internal `_Equip/_Unequip` / same validation rule
- `InventoryManager`
  - `SetHeroEquip(heroId, slotType, equipUid)`
  - `RemoveHeroEquip(heroId, slotType)`

---

## 3. Rules

- `SLOT_TYPE.NONE`은 미장착 상태 전용이다.
- 장착 시 `slotType != NONE`이어야 한다.
- `ITEM_EQUIP.equip_type`로 `TB_EQUIP_SLOT.Get(equipType)`를 조회한다.
- 요청 slot은 `allowed_slots`에 포함돼야 한다.
- 같은 equip을 다른 허용 slot으로 이동하는 것은 허용된다.
- 대상 slot에 기존 장비가 있으면 교체된다.
- 이미 다른 hero에 장착된 equip은 기존 owner에서 제거 후 이동한다.
- `two_handed=true` 장비를 `HAND_MAIN`에 장착하면 현재 `HAND_SUB` 장비를 자동 해제한다.
- `HAND_MAIN`에 양손 장비가 있는 상태에서 `HAND_SUB` 장착 시도는 실패한다.

---

## 4. Message Rule

자동 해제는 inventory mutation이다. `InventoryManager.SetHeroEquip()`는 아래 notify를 책임진다.

- 장착된 equip: `ITEM_EQUIP_CHANGED`
- 교체로 빠진 기존 target-slot equip: `ITEM_EQUIP_CHANGED`
- 양손 규칙으로 자동 해제된 sub-hand equip: `ITEM_EQUIP_CHANGED`
- hero loadout 변경이 발생한 hero: `ITEM_HERO_CHANGED`
- cross-hero 이동이면 이전 owner hero에도 `ITEM_HERO_CHANGED`

장착/해제로 inventory membership은 바뀌지 않으므로 `ITEM_*_LIST_CHANGED`는 발행하지 않는다.

---

## 5. Save/Load

- hero equip snapshot key는 `SLOT_TYPE`다.
- JSON 저장은 numeric enum string key를 유지한다.
- load는 numeric string key와 enum name string key를 모두 허용한다.
- `NONE` key는 저장/복원 대상이 아니다.

---

## 6. Mirror Policy

이 규칙을 수정하면 아래를 같이 맞춘다.

- `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/`
- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/GamePackage/Runtime/Ability/`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Inventory/`
- `framework-ts/module/devian-domain-game/features/ability/`

---

## 7. Related

- [12-game-ability](../12-game-ability/SKILL.md)
- [15-game-ability-factory](../15-game-ability-factory/SKILL.md)
- [../../50-mobile-package/22-inventory-system/03-ssot](../../50-mobile-package/22-inventory-system/03-ssot/SKILL.md)
- [../../50-mobile-package/22-inventory-system/10-inventory-manager](../../50-mobile-package/22-inventory-system/10-inventory-manager/SKILL.md)
- [../../50-mobile-package/22-inventory-system/11-inventory-storage](../../50-mobile-package/22-inventory-system/11-inventory-storage/SKILL.md)
