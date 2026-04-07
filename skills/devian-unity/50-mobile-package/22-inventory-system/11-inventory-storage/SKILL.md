# 11-inventory-storage

Status: ACTIVE
AppliesTo: v10

InventoryStorage는 InventoryManager의 **live runtime 데이터 컨테이너**이다.
통화 잔고 / 장비(AbilityItemEquip) / 카드(AbilityItemCard) / 재료(AbilityItemMaterial) / 영웅(AbilityItemHero)을 통합 관리한다.

InventoryStorage는 InventoryManager가 소유하며 `Devian.Samples.MobilePackage` asmdef에 속한다.
외부 시스템의 public boundary는 `InventoryManager`이고, persistence boundary는 `InventorySnapshot`이다.

---

## 1. InventoryStorage

코드는 실제 `InventoryStorage.cs` 참조. `sealed class`이다.

- `CurrencyBalances` — 내부 `Dictionary<CURRENCY_TYPE, long>` 기반 잔고 상태
- `Equipments` — `Dictionary<string, AbilityItemEquip>` (itemUid → 장비)
- `Cards` — `Dictionary<string, AbilityItemCard>` (item_id → 카드)
- `Materials` — `Dictionary<string, AbilityItemMaterial>` (item_id → 재료)
- `Heroes` — `Dictionary<string, AbilityItemHero>` (item_id → 영웅)
- `Rentals` — `Dictionary<string, long>` (item_id → expiresAtClientUtcMs)
- `Passes` — `Dictionary<string, bool>` (item_id → owned)
- `GetEquip/AddEquip/RemoveEquip` — 장비 CRUD (key=itemUid)
- `GetEquipsByItemId` — itemId로 인스턴스 목록 조회
- `GetCard/AddCard` — 카드 CRUD
- `GetMaterial/AddMaterial` — 재료 CRUD
- `GetHero/AddHero` — 영웅 CRUD
- `SetRental(id, expiresAtClientUtcMs)` / `GetRentalExpiry(id)` / `HasActiveRental(id)` / `GetRentalRemainingMs(id)` / `RemoveRental(id)` — 렌탈 CRUD
- `SetPass(id, owned)` / `HasPass(id)` / `RemovePass(id)` — 시즌패스 CRUD
- `TreasureCurrent` — `InventoryTreasureCurrent` (Exp/Level, sealed POCO)
- `TreasureCounts` — `Dictionary<TREASURE_GRADE_TYPE, int>` (grade별 보유 chest count)
- `GetTreasureCount(gradeType)` / `AddTreasure(gradeType, amount)` / `SetTreasureCount(gradeType, count)` — treasure count CRUD
- `AddTreasureExp(amount)` — delegates to `TreasureCurrent.Exp`
- `ResetTreasure(level, exp)` — delegates to `TreasureCurrent.Reset(...)`
- `CopyFrom(source)` — validated temp state를 live storage에 복사
- Pass 변경 알림 publish는 `InventoryManager`가 담당한다 (trigger 직접 노출 금지).
- 초기 보상 지급 트리거는 `InventoryStorage`가 아니라 `RewardManager.FirstInitAsync()`에서 처리한다 (`FirstRewardSettings` source).
- ~~`ToJson()`~~ — **삭제됨**. [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 `SaveDataJsonCodec`으로 이전.
- ~~`FromJson(string json)`~~ — **삭제됨**. [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 `SaveDataJsonCodec`으로 이전.

### 장비 장착

장비 장착/해제의 저장 로직은 `AbilityItemHero._SetEquip/_RemoveEquip`이 담당한다.
- 이 경로는 hero loadout metadata와 equip owner metadata만 갱신한다.
- 이미 다른 hero에 장착된 equip 이동은 `InventoryStorage.Equip()`가 기존 owner를 먼저 정리한 뒤 위임하는 경로만 사용한다.
- equip stat 계산은 `AbilityUnitHero._Equip/_Unequip`에서 수행한다.
InventoryStorage는 hero/equip 조회 + 위임하는 **편의 메서드**를 제공한다.

- `RemoveEquip`은 장착 상태면 hero 슬롯 맵까지 함께 정리한 뒤 제거한다.
- `Equip(heroId, equipSlot, equipUid)` / `Unequip(heroId, equipSlot)` 편의 메서드를 제공한다.
- slot key는 `SLOT_TYPE`이며 `NONE`은 유효한 장착 slot이 아니다.
- two-handed main 장착에 따른 `HAND_SUB` 자동 해제는 최종적으로 `AbilityItemHero._SetEquip()` 규칙에 따른다.

---

## 2. InventoryManager 관계

| 타입 | 책임 |
|---|---|
| `InventoryManager` | InventoryStorage 소유, 타입별 Apply/Revoke/Query API 제공 |
| `InventoryStorage` | CurrencyBalances (`Dictionary<CURRENCY_TYPE, long>`), Equipments (itemUid → AbilityItemEquip), Cards (item_id → AbilityItemCard), Materials (item_id → AbilityItemMaterial), Heroes (item_id → AbilityItemHero), Rentals (item_id → expiresAtClientUtcMs), Passes (item_id → owned), TreasureCurrent (`InventoryTreasureCurrent`), TreasureCounts (TREASURE_GRADE_TYPE → int) |
| `AbilityItemEquip` | OwnerUnitId/OwnerSlotType(별도 필드) + 능력치(STAT_TYPE 기반) 관리 |
| `AbilityItemCard` | 수량(`STAT_TYPE.ITEM_AMOUNT`) + 능력치(STAT_TYPE 기반) 관리 |
| `AbilityItemMaterial` | 수량(`STAT_TYPE.ITEM_AMOUNT`) + 능력치(STAT_TYPE 기반) 관리 |
| `AbilityItemHero` | 수량(`STAT_TYPE.ITEM_AMOUNT`) + 영웅 장비 슬롯(`Dict<SLOT_TYPE, AbilityItemEquip>`) 관리 |

- `InventoryManager`가 `InventoryStorage`를 소유한다 (싱글톤 등록 안 함).
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `item_id`에 여러 인스턴스가 존재할 수 있다.
- `ApplyCard(item_id, amount)` / `AddCardAmount(item_id, delta)` — `_storage.Cards`에 AbilityItemCard를 추가하고 `AddAmount(delta)`로 수량을 변경한다.
- `ApplyMaterial(item_id, amount)` / `AddMaterialAmount(item_id, delta)` — `_storage.Materials`에 AbilityItemMaterial을 추가하고 `AddAmount(delta)`로 수량을 변경한다.
- `ApplyEquip(item_id)` — 새 `itemUid`(GUID)로 AbilityItemEquip을 생성하여 `_storage.Equipments`에 추가한다.
- `ApplyHero(item_id, amount)` / `AddHeroAmount(item_id, delta)` — `_storage.Heroes`에 AbilityItemHero를 추가하고 `AddAmount(delta)`로 수량을 변경한다.
- `ApplyRental(item_id, durationMs)` — `_storage.SetRental(id, max(currentExpiry, now)+duration)`로 로컬 만료 시각을 설정/연장한다.
- `SetPassOwnership(item_id)` — `_storage.SetPass(id, true)`로 소유권을 설정한다.
- `ApplyTreasure(gradeType, amount)` — `_storage.AddTreasure(gradeType, amount)`로 chest count를 누적한다.
- publish는 `InventoryStorage`가 아니라 `InventoryManager`가 담당한다.
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

- `InventoryManager`가 public query boundary를 제공한다.
- 외부 시스템은 `GetEquipments/GetCards/GetMaterials/GetHeroes/GetRentals/GetPasses/GetRentalRemainingMs/HasPass/HasActiveRental` 같은 manager helper를 사용한다.
- 신규 코드에서 `InventoryStorage` 직접 조회/변경은 금지한다.
- load/import/clear는 temp `InventorySnapshot`을 만든 뒤 `InventoryManager.ReplaceState/ClearState`로 반영한다.
- 카드/재료/영웅은 amount가 0이 되면 storage에서 제거한다.

---

## 3. Implementation Location (3-path mirror)

### 파일 위치 (MobilePackage 샘플)

```
MobilePackage/Runtime/Inventory/
├── InventoryManager.cs          (10-inventory-manager)
├── InventoryStorage.cs
├── InventorySnapshot.cs         (save/load DTO)
├── InventoryStaminaController.cs (14-inventory-stamina-controller)
└── InventoryMessageTrigger.cs   (16-inventory-message-trigger)
```

3-path mirror ([정책](../../../04-package-policy/SKILL.md)):
- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Inventory/`

NOTE: `ItemData` 클래스는 `AbilityItemEquip`에 통합되어 삭제되었다. `BagItems`는 `Equipments`로 리네임되었다.

---

## 4. asmdef

`Devian.Samples.MobilePackage.asmdef`에 포함된 참조:
- `Devian.Domain.Game` (STAT_TYPE — AbilityItemEquip → AbilityBase 경유)

---

## 5. namespace

```csharp
namespace Devian
```

(Samples 정책: 단일 namespace `Devian`)

---

## 6. Hard Rules

- InventoryStorage는 **sealed POCO 클래스**이다 (MonoBehaviour 금지).
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `item_id`에 여러 인스턴스가 존재할 수 있다.
- currency 잔고는 `InventoryStorage` 내부 딕셔너리와 helper(`GetCurrencyAmount/TryAddCurrency`)로 직접 관리한다.
- `Equipments` key = `itemUid` (string). value = `AbilityItemEquip`.
- `Cards` key = `item_id` (string). value = `AbilityItemCard`.
- `Materials` key = `item_id` (string). value = `AbilityItemMaterial`.
- `Heroes` key = `item_id` (string). value = `AbilityItemHero`.
- `Rentals` key = `item_id` (string key). value = `long` expiresAtClientUtcMs.
- `Passes` key = `item_id` (string key). value = `bool` owned.
- `TreasureCurrent` = `InventoryTreasureCurrent` (sealed POCO, Exp/Level).
- `TreasureCounts` key = `TREASURE_GRADE_TYPE` (NONE 제외). value = `int` (보유 chest count).
- treasure count와 exp는 음수 불가 (0 이하 clamp).
- treasure current 상태는 grade별 분리 없이 단일 Exp/Level만 사용한다.
- 장비 장착/해제의 핵심 로직은 `AbilityItemHero`가 담당한다. InventoryStorage는 편의 메서드(`Equip`/`Unequip`)로 위임한다.
- InventoryStorage는 InventoryManager가 소유한다 (싱글톤 등록 안 함).
- "Sample" 접두사 금지 (정책).

---

## 7. JSON 직렬화 — SaveDataJsonCodec으로 이전됨

> **변경**: `ToJson()` / `FromJson()` 메서드는 **삭제**되었다.
> 직렬화 책임은 [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 **`SaveDataJsonCodec`**가 담당한다.

InventoryStorage는 live runtime helper를 제공한다.
`SaveDataJsonCodec`은 live storage를 직접 직렬화하지 않고 `InventorySnapshot`을 사용한다.
`InventoryManager.CreateSnapshot()`이 live storage를 snapshot으로 투영하고, `InventoryManager.ReplaceState()`가 snapshot을 validate/build 후 live storage로 적용한다.

JSON 스키마: [03-ssot](../03-ssot/SKILL.md) 참조.
`SaveDataJsonCodec` 설계: [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) 참조.

---

## 8. Related

- [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md) — AbilityBase, AbilityItemEquip, AbilityItemCard, AbilityItemMaterial, AbilityItemHero, AbilityUnitHero
- [13-game-stat-type](../../../../devian/21-domain-game/13-game-stat-type/SKILL.md) — STAT_TYPE enum
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유자, 수량 SSOT)
- [12-inventory-wallet](../12-inventory-wallet/SKILL.md) — Currency State Flattening (legacy wallet wrapper 제거)
- [03-ssot](../03-ssot/SKILL.md) — Inventory State/Apply Rules
- [00-overview](../00-overview/SKILL.md) — Inventory System 개요
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 담당
