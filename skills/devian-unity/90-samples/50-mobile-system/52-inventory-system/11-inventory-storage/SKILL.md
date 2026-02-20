# 11-inventory-storage

Status: ACTIVE
AppliesTo: v10

InventoryStorage는 InventoryManager의 **인벤토리 데이터 컨테이너**이다.
통화 잔고 / 장비(AbilityEquip) / 카드(AbilityCard) / 영웅(AbilityUnitHero)을 통합 관리한다.

InventoryStorage는 InventoryManager가 소유하며 `Devian.Samples.MobileSystem` asmdef에 속한다.

---

## 1. InventoryStorage

코드는 실제 `InventoryStorage.cs` 참조. 주요 필드/메서드:

- `Wallet` — `Dictionary<string, long>` (currencyId → 잔고)
- `Equipments` — `Dictionary<string, AbilityEquip>` (itemUid → 장비)
- `Cards` — `Dictionary<string, AbilityCard>` (cardId → 카드)
- `Heroes` — `Dictionary<string, AbilityUnitHero>` (heroId → 영웅)
- `GetEquip/AddEquip/RemoveEquip` — 장비 CRUD (key=itemUid)
- `GetEquipsByEquipId` — equipId로 인스턴스 목록 조회
- `Equip(heroId, equipSlot, equipUid)` — 편의 메서드: itemUid(PK)로 장비 조회 후 hero.Equip 위임
- `Unequip(heroId, equipSlot)` — 편의 메서드: hero.Unequip 위임
- `GetCard/AddCard` — 카드 CRUD
- `GetHero/AddHero` — 영웅 CRUD
- `ToJson()` — 전체 상태 → JSON string 직렬화
- `FromJson(string json)` — JSON string → 전체 상태 복원 (Clear 후 재구성)

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
| `InventoryManager` | `GetAmount`, InventoryStorage 소유, AddRewards 검증/연동 |
| `InventoryStorage` | Wallet (currencyId → long), Equipments (itemUid → AbilityEquip), Cards (cardId → AbilityCard), Heroes (heroId → AbilityUnitHero) |
| `AbilityEquip` | OwnerUnitId/OwnerSlotNumber(별도 필드) + 능력치(StatType 기반) 관리 |
| `AbilityCard` | 수량(`StatType.CardAmount`) + 능력치(StatType 기반) 관리 |
| `AbilityUnitHero` | 수량(`StatType.UnitAmount`) + 영웅 능력치(StatType 기반) + 장비 슬롯(`Dict<int, AbilityEquip>`) 관리 |

- `InventoryManager`가 `InventoryStorage`를 소유한다 (싱글톤 등록 안 함).
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- `AddRewards` 시 `RewardType.Card`이면 `_storage.Cards`에 AbilityCard를 추가하고 `AddAmount(delta)`로 수량 누적한다.
- `AddRewards` 시 `RewardType.Equip`이면 새 `itemUid`(GUID)로 AbilityEquip을 생성하여 `_storage.Equipments`에 추가한다.
- `AddRewards` 시 `RewardType.Hero`이면 `_storage.Heroes`에 AbilityUnitHero를 추가하고 `AddStat(StatType.UnitAmount, delta)`로 수량 누적한다.

```csharp
public sealed class InventoryManager : MonoBehaviour
{
    readonly InventoryStorage _storage = new();
    public InventoryStorage Storage => _storage;

    // ... CompoSingleton<InventoryManager> 패턴 ...
}
```

---

## 3. Implementation Location (SSOT)

### 파일 위치 (MobileSystem 샘플)

```
MobileSystem/Runtime/InventoryManager/
├── InventoryManager.cs   (10-inventory-manager)
└── InventoryStorage.cs
```

3경로 미러:
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/InventoryManager/`

NOTE: `ItemData` 클래스는 `AbilityEquip`에 통합되어 삭제되었다. `BagItems`는 `Equipments`로 리네임되었다.

---

## 4. asmdef

`Devian.Samples.MobileSystem.asmdef`에 이미 포함된 참조로 충분하다:
- `Devian.Domain.Game` (StatType — AbilityEquip → AbilityBase 경유)

추가 참조 불필요.

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
- `Wallet` key = `currencyId` (string). value = `long` 잔고.
- `Equipments` key = `itemUid` (string). value = `AbilityEquip`.
- `Cards` key = `cardId` (string). value = `AbilityCard`.
- `Heroes` key = `heroId` (string). value = `AbilityUnitHero`.
- 장비 장착/해제의 핵심 로직은 `AbilityUnitHero`가 담당한다. InventoryStorage는 편의 메서드(`Equip`/`Unequip`)로 위임한다.
- InventoryStorage는 InventoryManager가 소유한다 (싱글톤 등록 안 함).
- "Sample" 접두사 금지 (정책).

---

## 7. JSON 직렬화

InventoryStorage는 string 기반 JSON 직렬화/역직렬화를 제공한다 (`Newtonsoft.Json.Linq`).

- `ToJson()` — `string` 반환. 내부에서 JObject 빌드 후 `.ToString()`. wallet/equipments/cards/heroes 전체 상태 포함.
- `FromJson(string json)` — `JObject.Parse(json)` 후 `Clear()` → 전체 상태 복원. 테이블 참조는 `TB_EQUIP.Get`/`TB_CARD.Get`/`TB_UNIT_HERO.Get`으로 재로딩.

**역직렬화 순서**: wallet → equipments → cards → heroes (heroes가 마지막: equip 슬롯 참조 복원에 mEquipments 필요).

**hero stats 주의**: `Init()` 이 `UnitHpMax`를 AddStat하므로, JSON stats 복원 전에 `ClearStats()` 호출 필수.

JSON 스키마: [03-ssot](../03-ssot/SKILL.md) 참조.

---

## 8. Related

- [12-game-ability](../../../40-game-system/12-game-ability/SKILL.md) — AbilityBase, AbilityEquip, AbilityCard, AbilityUnitHero (Equipments/Cards/Heroes 직접 관리)
- [13-game-stat-type](../../../40-game-system/13-game-stat-type/SKILL.md) — StatType enum
- [10-inventory-manager](../10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유자, 수량 SSOT)
- [03-ssot](../03-ssot/SKILL.md) — Inventory State/Apply Rules
- [00-overview](../00-overview/SKILL.md) — Inventory System 개요
