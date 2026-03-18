# 11-inventory-storage

Status: ACTIVE
AppliesTo: v10

InventoryStorage는 InventoryManager의 **인벤토리 데이터 컨테이너**이다.
통화 잔고 / 장비(AbilityEquip) / 카드(AbilityCard) / 영웅(AbilityUnitHero)을 통합 관리한다.

InventoryStorage는 InventoryManager가 소유하며 `Devian.Samples.MobilePackage` asmdef에 속한다.

---

## 1. InventoryStorage

코드는 실제 `InventoryStorage.cs` 참조. `sealed class`이다.

- `Wallet` — `InventoryWallet` (내부 `Dictionary<CURRENCY_TYPE, long>` 기반 잔고 컨테이너)
- `Equipments` — `Dictionary<string, AbilityEquip>` (itemUid → 장비)
- `Cards` — `Dictionary<string, AbilityCard>` (cardId → 카드)
- `Heroes` — `Dictionary<string, AbilityUnitHero>` (heroId → 영웅)
- `Rentals` — `Dictionary<string, long>` (rentalTypeId → expiresAtClientUtcMs)
- `Passes` — `Dictionary<string, bool>` (passId → owned)
- `GetEquip/AddEquip/RemoveEquip` — 장비 CRUD (key=itemUid)
- `GetEquipsByEquipId` — equipId로 인스턴스 목록 조회
- `Equip(heroId, equipSlot, equipUid)` — 편의 메서드: itemUid(PK)로 장비 조회 후 hero.Equip 위임
- `Unequip(heroId, equipSlot)` — 편의 메서드: hero.Unequip 위임
- `GetCard/AddCard` — 카드 CRUD
- `GetHero/AddHero` — 영웅 CRUD
- `SetRental(id, expiresAtClientUtcMs)` / `GetRentalExpiry(id)` / `HasActiveRental(id)` / `GetRentalRemainingMs(id)` / `RemoveRental(id)` — 렌탈 CRUD
- `SetPass(id, owned)` / `HasPass(id)` / `RemovePass(id)` — 시즌패스 CRUD
- `TreasureCurrent` — `InventoryTreasureCurrent` (Exp/Level, sealed POCO)
- `TreasureCounts` — `Dictionary<TREASURE_GRADE_TYPE, int>` (grade별 보유 chest count)
- `GetTreasureCount(gradeType)` / `AddTreasure(gradeType, amount)` / `SetTreasureCount(gradeType, count)` — treasure count CRUD
- `AddTreasureExp(amount)` — delegates to `TreasureCurrent.Exp`
- `ResetTreasure(level, exp)` — delegates to `TreasureCurrent.Reset(...)`
- Pass 변경 알림 publish는 `InventoryManager`가 담당한다 (trigger 직접 노출 금지).
- 초기 보상 지급 트리거는 `InventoryStorage`가 아니라 `RewardManager.FirstInitAsync()`에서 처리한다 (`FirstRewardSettings` source).
- ~~`ToJson()`~~ — **삭제됨**. [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 `SaveDataJsonCodec`으로 이전.
- ~~`FromJson(string json)`~~ — **삭제됨**. [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 `SaveDataJsonCodec`으로 이전.

### 장비 장착

장비 장착/해제의 실제 로직은 `AbilityUnitHero.Equip/Unequip`이 담당한다.
InventoryStorage는 hero/equip 조회 + 위임하는 **편의 메서드**를 제공한다.

- `Equip(string heroId, int equipSlot, string equipUid)` — itemUid(PK)로 장비를 조회하여 hero.Equip 위임. hero 또는 장비가 없으면 false.
- `Unequip(string heroId, int equipSlot)` — hero.Unequip 위임.
- `RemoveEquip`은 장착 상태면 `equip.ClearOwner()` 호출 후 제거한다.

---

## 2. InventoryManager 관계

| 타입 | 책임 |
|---|---|
| `InventoryManager` | InventoryStorage 소유, 타입별 Apply/Revoke/Query API 제공 |
| `InventoryStorage` | Wallet (`InventoryWallet`), Equipments (itemUid → AbilityEquip), Cards (cardId → AbilityCard), Heroes (heroId → AbilityUnitHero), Rentals (rentalTypeId → expiresAtClientUtcMs), Passes (passId → owned), TreasureCurrent (`InventoryTreasureCurrent`), TreasureCounts (TREASURE_GRADE_TYPE → int) |
| `AbilityEquip` | OwnerUnitId/OwnerSlotNumber(별도 필드) + 능력치(STAT_TYPE 기반) 관리 |
| `AbilityCard` | 수량(`STAT_TYPE.CARD_AMOUNT`) + 능력치(STAT_TYPE 기반) 관리 |
| `AbilityUnitHero` | 수량(`STAT_TYPE.UNIT_AMOUNT`) + 영웅 능력치(STAT_TYPE 기반) + 장비 슬롯(`Dict<int, AbilityEquip>`) 관리 |

- `InventoryManager`가 `InventoryStorage`를 소유한다 (싱글톤 등록 안 함).
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- `ApplyCard(cardId, amount)` — `_storage.Cards`에 AbilityCard를 추가하고 `AddAmount(delta)`로 수량 누적한다.
- `ApplyEquip(equipId)` — 새 `itemUid`(GUID)로 AbilityEquip을 생성하여 `_storage.Equipments`에 추가한다.
- `ApplyHero(heroId, amount)` — `_storage.Heroes`에 AbilityUnitHero를 추가하고 `AddStat(STAT_TYPE.UNIT_AMOUNT, delta)`로 수량 누적한다.
- `ApplyRental(rentalId, durationMs)` — `_storage.SetRental(id, max(currentExpiry, now)+duration)`로 로컬 만료 시각을 설정/연장한다.
- `SetPassOwnership(passId)` — `_storage.SetPass(id, true)`로 소유권을 설정한다.
- `ApplyTreasure(gradeType, amount)` — `_storage.AddTreasure(gradeType, amount)`로 chest count를 누적한다.
- 시즌패스 상태 변경 시 `INVENTORY_MESSAGE_TYPE.PASS_CHANGED`를 publish할 수 있어야 한다(실행 위치: `InventoryManager`).

```csharp
public sealed class InventoryManager : MonoBehaviour
{
    readonly InventoryStorage _storage = new();
    public InventoryStorage Storage => _storage;

    // ... CompoSingleton<InventoryManager> 패턴 ...
}
```

---

## 3. Implementation Location (3-path mirror)

### 파일 위치 (MobilePackage 샘플)

```
MobilePackage/Runtime/Inventory/
├── InventoryManager.cs   (10-inventory-manager)
└── InventoryStorage.cs
```

3-path mirror ([정책](../../../04-package-policy/SKILL.md)):
- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Inventory/`

NOTE: `ItemData` 클래스는 `AbilityEquip`에 통합되어 삭제되었다. `BagItems`는 `Equipments`로 리네임되었다.

---

## 4. asmdef

`Devian.Samples.MobilePackage.asmdef`에 포함된 참조:
- `Devian.Domain.Game` (STAT_TYPE — AbilityEquip → AbilityBase 경유)

---

## 5. namespace

```csharp
namespace Devian
```

(Samples 정책: 단일 namespace `Devian`)

---

## 6. Hard Rules

- InventoryStorage는 **sealed POCO 클래스**이다 (MonoBehaviour 금지).
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- `Wallet`은 `InventoryWallet` API(`Get/TryAdd`)를 통해 `CURRENCY_TYPE`별 잔고를 관리한다.
- `Equipments` key = `itemUid` (string). value = `AbilityEquip`.
- `Cards` key = `cardId` (string). value = `AbilityCard`.
- `Heroes` key = `heroId` (string). value = `AbilityUnitHero`.
- `Rentals` key = `rentalTypeId` (string key). value = `long` expiresAtClientUtcMs.
- `Passes` key = `passId` (string key). value = `bool` owned.
- `TreasureCurrent` = `InventoryTreasureCurrent` (sealed POCO, Exp/Level).
- `TreasureCounts` key = `TREASURE_GRADE_TYPE` (NONE 제외). value = `int` (보유 chest count).
- treasure count와 exp는 음수 불가 (0 이하 clamp).
- treasure current 상태는 grade별 분리 없이 단일 Exp/Level만 사용한다.
- 장비 장착/해제의 핵심 로직은 `AbilityUnitHero`가 담당한다. InventoryStorage는 편의 메서드(`Equip`/`Unequip`)로 위임한다.
- InventoryStorage는 InventoryManager가 소유한다 (싱글톤 등록 안 함).
- "Sample" 접두사 금지 (정책).

---

## 7. JSON 직렬화 — SaveDataJsonCodec으로 이전됨

> **변경**: `ToJson()` / `FromJson()` 메서드는 **삭제**되었다.
> 직렬화 책임은 [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 **`SaveDataJsonCodec`**가 담당한다.

InventoryStorage는 **ReadOnly 프로퍼티**(`Wallet`, `Equipments`, `Cards`, `Heroes`, `Rentals`, `Passes`, `TreasureCurrent`, `TreasureCounts`)와 **CRUD 메서드**만 제공한다.
`SaveDataJsonCodec`이 이 프로퍼티/메서드를 사용하여 직렬화/역직렬화를 수행한다.

JSON 스키마: [03-ssot](../03-ssot/SKILL.md) 참조.
`SaveDataJsonCodec` 설계: [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) 참조.

---

## 8. Related

- [12-game-ability](../../../../devian/21-domain-game/12-game-ability/SKILL.md) — AbilityBase, AbilityEquip, AbilityCard, AbilityUnitHero (Equipments/Cards/Heroes 직접 관리)
- [13-game-stat-type](../../../../devian/21-domain-game/13-game-stat-type/SKILL.md) — STAT_TYPE enum
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유자, 수량 SSOT)
- [12-inventory-wallet](../12-inventory-wallet/SKILL.md) — InventoryWallet (Dictionary 기반 Wallet + JEWEL 파생 조회)
- [03-ssot](../03-ssot/SKILL.md) — Inventory State/Apply Rules
- [00-overview](../00-overview/SKILL.md) — Inventory System 개요
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 담당
