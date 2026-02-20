# 10-inventory-manager


Status: ACTIVE
AppliesTo: v10


InventoryManager(구현 규약)는 `RewardData[]` 입력을 받아 인벤토리 상태에 적용(Apply)한다.

- `type=RewardType.Currency`: `currencyType -> amount(long)` 잔고 누적
- `type=RewardType.Equip`: 매 Apply마다 새 인스턴스(`itemUid=GUID` pk) 생성, amount 무시
- `type=RewardType.Card`: `cardId(pk)` → `AbilityCard.Amount` (`StatType.CardAmount`) 누적
- `type=RewardType.Hero`: `heroId(pk)` → `AbilityUnitHero` (`StatType.UnitAmount`) 누적

InventoryManager는 **단일 concrete 클래스**이다.
InventoryStorage를 소유하며, AddRewards 시 Equipments/Cards/Heroes에 AbilityEquip/AbilityCard/AbilityUnitHero를 추가/갱신한다.


---


## Class Design

```csharp
using Devian.Domain.Common;

public sealed class InventoryManager : MonoBehaviour
{
    readonly InventoryStorage _storage = new();
    public InventoryStorage Storage => _storage;

    public static InventoryManager Instance
        => CompoSingleton<InventoryManager>.Instance;

    protected void Awake()
    {
        CompoSingleton<InventoryManager>.Register(this);
    }

    // ── Public API ──

    public CommonResult AddRewards(RewardData[] rewards) { ... }

    public long GetAmount(string type, string id) { ... }
}
```

- `InventoryManager : MonoBehaviour` (sealed, concrete)
- `CompoSingleton<InventoryManager>` (1-param)으로 등록
- Registry key: `InventoryManager`
- 다른 매니저에서 접근: `Singleton.Get<InventoryManager>()`


---


## Singleton

```csharp
CompoSingleton<InventoryManager>.Instance
```


---


## Responsibilities (정본)

- `RewardData[]`를 AddRewards로 적용한다.
  - `type=RewardType.Currency`와 `type=RewardType.Equip`과 `type=RewardType.Card`와 `type=RewardType.Hero`의 처리 로직은 분기된다(정본).
  - `type=RewardType.Equip`일 때 새 itemUid(GUID)로 AbilityEquip 인스턴스를 생성하여 InventoryStorage.Equipments에 추가한다.
  - `type=RewardType.Card`일 때 InventoryStorage.Cards에 AbilityCard를 추가/갱신한다.
  - `type=RewardType.Hero`일 때 InventoryStorage.Heroes에 AbilityUnitHero를 추가/갱신한다.
  - 입력 검증 실패 시 `CommonResult.Failure`를 반환하고 상태를 변경하지 않는다.
  - 성공 시 `CommonResult.Ok()`를 반환한다.
- 수량 조회를 제공한다.
  - `type=RewardType.Currency`: 잔고 조회
  - `type=RewardType.Equip`: 해당 `equipId`를 가진 인스턴스 수 반환
  - `type=RewardType.Card`: 해당 `cardId(pk)` 수량 조회
  - `type=RewardType.Hero`: 해당 `heroId(pk)` 수량 조회
- InventoryStorage를 소유한다.
- 변경 이벤트를 제공한다(개념).

NOTE:
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- `GetAmount(Equip, equipId)`는 해당 equipId를 가진 인스턴스 수를 반환한다.
- 카드 수량 SSOT = `AbilityCard.Amount` (= `this[StatType.CardAmount]`).
- 영웅 수량 = `AbilityUnitHero[StatType.UnitAmount]`.
- `AbilityEquip`이 능력치를 관리한다 (`ItemData`는 `AbilityEquip`에 통합됨). 장비 장착은 `AbilityUnitHero`가 담당한다.
- Apply는 카드/영웅의 Amount stat만 변경한다 (다른 stat은 보존). 장비는 매번 새 인스턴스를 생성한다.

비책임:
- 멱등/기록/복구는 호출자(Mission/Purchase)가 책임진다.


---


## Dependencies (개념)

- InventoryManager는 SaveDataManager를 직접 참조하지 않는다.
- SaveDataManager는 InventoryManager/Inventory 스키마를 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.


---


## Public API (설계)

- `AddRewards(RewardData[] rewards) -> CommonResult`
  - 입력 전체를 선검증한다.
  - 하나라도 invalid면 `CommonResult.Failure(error)`를 반환하고 상태를 변경하지 않는다.
  - 전체 valid이면 Apply 한다(멱등 아님):
    - `type=RewardType.Currency`: `_storage.AddCurrency(currencyType, amount)`
    - `type=RewardType.Equip`: 새 `itemUid`(GUID)로 AbilityEquip 생성 + `_storage.AddEquip(itemUid, ability)`. amount 무시(항상 1개)
    - `type=RewardType.Card`: `_storage.Cards`에 AbilityCard 추가(없으면 생성) + `AbilityCard.AddAmount(amount)`
    - `type=RewardType.Hero`: `_storage.Heroes`에 AbilityUnitHero 추가(없으면 생성) + `AddStat(StatType.UnitAmount, amount)`
  - 성공 시 `CommonResult.Ok()`를 반환한다.
- `GetAmount(string type, string id) -> long`
  - `(type,id)`에 대한 현재 수량을 반환한다.


---


## Validation Rules (정본)

- `rewards == null`이면 invalid다.
- 각 reward에 대해 아래 조건을 모두 만족해야 valid다.
  - `type`은 `RewardType.Currency`, `RewardType.Equip`, `RewardType.Card`, `RewardType.Hero` 중 하나여야 한다.
  - `id`는 null/empty/whitespace가 아니어야 한다.
  - `amount >= 0` 이어야 한다.
- `rewards.Length == 0`은 valid no-op으로 처리한다(`CommonResult.Ok()` 반환).
- `amount == 0`은 valid no-op delta로 처리한다(에러 아님).


## Apply Atomicity (정본)

- `AddRewards`는 원자적으로 동작한다.
- 입력 중 invalid가 하나라도 있으면 전체 실패한다.
- 전체 실패 시 내부 상태(`_storage.Wallet`, `_storage.Equipments`, `_storage.Cards`, `_storage.Heroes`)는 호출 전과 동일해야 한다.


## Error Mapping (정본)

- `AddRewards` 실패는 `CommonError(CommonErrorType, message, details)`를 사용한다.
- 권장 `CommonErrorType`:
  - `INVENTORY_DELTAS_NULL`
  - `INVENTORY_DELTA_TYPE_INVALID`
  - `INVENTORY_DELTA_ID_EMPTY`
  - `INVENTORY_DELTA_AMOUNT_NEGATIVE`
- 위 코드가 아직 없으면 `ERROR_COMMON`(SSOT)에 먼저 추가하고 생성 파이프라인으로 `CommonErrorType`을 갱신한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `ERROR_COMMON`


---


## Prerequisites (구현 전 사전 작업)

### 1) ERROR_COMMON에 Inventory 에러 코드 추가 — ✅ 완료

`CommonErrorType`에 아래 4개 코드가 추가/생성 완료되었다.

| 코드 | 용도 |
|---|---|
| `INVENTORY_DELTAS_NULL` | `rewards == null` |
| `INVENTORY_DELTA_TYPE_INVALID` | `type`이 `RewardType.Currency`/`RewardType.Equip`/`RewardType.Card`/`RewardType.Hero`가 아님 |
| `INVENTORY_DELTA_ID_EMPTY` | `id`가 null/empty/whitespace |
| `INVENTORY_DELTA_AMOUNT_NEGATIVE` | `amount < 0` |


---


## Internal State (설계)

```csharp
// InventoryManager 내부
readonly InventoryStorage _storage = new();
```

- `InventoryItem` 클래스는 사용하지 않는다.
- 통화 상태는 `_storage.Wallet[currencyId]` → `long`으로 관리한다.
- 장비 상태는 `_storage.Equipments[itemUid]` → `AbilityEquip`이 전담한다.
- 카드 상태는 `_storage.Cards[cardId]` → `AbilityCard`가 전담한다.
- 영웅 상태는 `_storage.Heroes[heroId]` → `AbilityUnitHero`가 전담한다.
- 장비 보유 = itemUid(들) 존재. `GetEquipsByEquipId(equipId)`로 equipId별 인스턴스 조회
- 카드 수량 = `this[StatType.CardAmount]` (AbilityCard)
- 영웅 수량 = `this[StatType.UnitAmount]` (AbilityUnitHero)
- 능력치 = `AbilityEquip` (StatType 기반 정규화)
- 장비 장착 = `AbilityUnitHero.Equip/Unequip` (AbilityEquip.OwnerUnitId/OwnerSlotNumber)


---


## asmdef

`Devian.Samples.MobileSystem.asmdef`에 포함된 참조:
- `Devian.Domain.Common` — `CommonResult`, `CommonError`, `CommonErrorType`
- `Devian.Domain.Game` — `StatType` (AbilityEquip → AbilityBase 경유, InventoryStorage 의존)
- `RewardData` 타입은 Reward 시스템 정본 규약(49-reward-system) 기반으로 사용한다.


---


## Implementation Location (SSOT)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/InventoryManager.cs`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/InventoryManager/InventoryManager.cs`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/InventoryManager/InventoryManager.cs`


---


## Notes

- 내부 구현 메서드는 Devian 정책에 따라 `_MethodName` 네이밍을 사용한다(구현 단계).
- `RewardData` 스키마 정본은 [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)다.
- Inventory 시스템은 위 정본을 입력 계약으로 참조한다.
- `RewardData` 런타임 타입 파일은 `RewardManager/RewardData.cs`(49-reward-system 미러 경로)에 위치한다.


---


## Related

- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage (소유 대상)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md) — RewardData 스키마 정본
- [03-ssot](../03-ssot/SKILL.md) — Inventory 상태/Apply 규칙 SSOT
- [01-policy](../01-policy/SKILL.md) — Inventory 하드룰
- [10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md) — RewardManager (AddRewards 위임 호출자)
- [15-singleton](../../../../10-foundation/15-singleton/SKILL.md) — CompoSingleton 규약
