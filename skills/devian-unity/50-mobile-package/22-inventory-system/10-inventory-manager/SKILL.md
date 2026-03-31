# 10-inventory-manager


Status: ACTIVE
AppliesTo: v10


InventoryManager(구현 규약)는 인벤토리 상태를 관리하는 시스템이다. 타입별 구체 API를 제공한다.

- `RewardData` 해석(type switch)은 InventoryManager의 책임이 아니다 → [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)가 담당.
- InventoryManager는 `RewardData`를 직접 참조하지 않는다.

InventoryManager는 **단일 concrete 클래스**이다.
InventoryStorage를 소유하며, 타입별 구체 API로 상태를 변경한다.


---


## Class Design

```csharp
using Devian.Domain.Common;

public sealed class InventoryManager : CompoSingleton<InventoryManager>
{
    readonly InventoryStorage _storage = new();
    readonly InventoryMessageTrigger _messageTrigger = new();
    public InventoryStorage Storage => _storage;

    public static InventoryManager Instance
        => CompoSingleton<InventoryManager>.Instance;

    // ── Apply API ──
    public CommonResult ApplyCurrency(CURRENCY_TYPE currencyType, long amount) { ... }
    public CommonResult ApplyEquip(string itemId, int amount) { ... }
    public CommonResult ApplyCard(string itemId, int amount) { ... }
    public CommonResult ApplyMaterial(string itemId, int amount) { ... }
    public CommonResult ApplyHero(string itemId, int amount) { ... }
    public CommonResult ApplyRental(string itemId) { ... }
    public CommonResult ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }
    public CommonResult SetPassOwnership(string itemId, bool owned) { ... }
    public CommonResult RemovePassOwnership(string itemId) { ... }

    // ── Revoke API ──
    public void RevokeCurrency(CURRENCY_TYPE currencyType, long amount) { ... }
    public void RevokeEquip(string itemId, int amount) { ... }
    public void RevokeCard(string itemId, int amount) { ... }
    public void RevokeMaterial(string itemId, int amount) { ... }
    public void RevokeHero(string itemId, int amount) { ... }
    public void RevokeRental(string itemId) { ... }
    public void RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }

    // ── Query API ──
    public long GetCurrencyAmount(CURRENCY_TYPE currencyType) { ... }
    public int GetEquipCount(string itemId) { ... }
    public long GetCardAmount(string itemId) { ... }
    public long GetMaterialAmount(string itemId) { ... }
    public long GetHeroAmount(string itemId) { ... }
    public bool HasActiveRental(string itemId) { ... }
    public bool HasPass(string itemId) { ... }
    public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType) { ... }

    // ── Message ──
    public void Subcribe(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Handler handler) { ... }
    public void UnSubcribe(EntityId ownerKey) { ... }
}
```

- `InventoryManager : CompoSingleton<InventoryManager>` (sealed, concrete)
- Registry key: `InventoryManager`
- 다른 매니저에서 접근: `Singleton.Get<InventoryManager>()`


---


## Singleton

```csharp
CompoSingleton<InventoryManager>.Instance
```


---


## Responsibilities (정본)

- 타입별 구체 API로 인벤토리 상태를 변경한다.
  - `ApplyCurrency`: 잔고 누적
  - `ApplyEquip`: 새 `itemUid`(GUID)로 AbilityItemEquip 인스턴스 생성, amount 횟수만큼
  - `ApplyCard`: AbilityItemCard 추가/갱신 (`STAT_TYPE.ITEM_AMOUNT` 누적)
  - `ApplyMaterial`: AbilityItemMaterial 추가/갱신 (`STAT_TYPE.ITEM_AMOUNT` 누적)
  - `ApplyHero`: AbilityItemHero 추가/갱신 (`STAT_TYPE.ITEM_AMOUNT` 누적)
  - `ApplyRental`: 로컬 만료 시각 설정/연장 (`max(currentExpiry, now) + 30days`)
  - `SetPassOwnership`: 소유권 설정 + 메시지 트리거
- `ApplyTreasure`: chest count 누적
- public Apply API는 boundary다. item ability 생성 실패/invalid input은 `CommonResult.Failure(...)`로 반환한다.
- internal helper는 이미 검증된 내부 invariant 위반에 한해 제한적으로 예외를 사용할 수 있다.
- 타입별 Revoke API로 회수한다.
- 타입별 Query API로 수량/상태를 조회한다.
- InventoryStorage를 소유한다.
- 변경 이벤트를 제공한다(개념).

비책임:
- `RewardData` 해석 (RewardManager 담당)
- 초기 보상 지급 (`FirstInitAsync`는 RewardManager 담당)
- 멱등/기록/복구는 호출자(Mission/Purchase)가 책임진다.


---


## Dependencies (개념)

- InventoryManager는 저장 시스템을 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.
- JSON 직렬화 규약은 [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)를 따른다.


---


## Public API (설계)

### Apply

- `ApplyCurrency(CURRENCY_TYPE currencyType, long amount) -> CommonResult` — `_storage.Wallet.TryAdd(currencyType, amount)`
- `ApplyEquip(string itemId, int amount) -> CommonResult` — amount 횟수만큼 새 `itemUid`(GUID)로 AbilityItemEquip 생성 + `_storage.AddEquip(itemUid, ability)`
- `ApplyCard(string itemId, int amount) -> CommonResult` — `_storage.Cards`에 AbilityItemCard 추가(없으면 생성) + `AbilityItemCard.AddAmount(amount)`
- `ApplyMaterial(string itemId, int amount) -> CommonResult` — `_storage.Materials`에 AbilityItemMaterial 추가(없으면 생성) + `AbilityItemMaterial.AddAmount(amount)`
- `ApplyHero(string itemId, int amount) -> CommonResult` — `_storage.Heroes`에 AbilityItemHero 추가(없으면 생성) + `AbilityItemHero.AddAmount(amount)`
- `ApplyRental(string itemId) -> CommonResult` — `_storage.SetRental(id, max(currentExpiry, now)+30days)`
- `SetPassOwnership(string itemId, bool owned) -> CommonResult` — `_storage.SetPass(itemId, owned)` + 메시지 트리거
- `RemovePassOwnership(string itemId) -> CommonResult` — `_storage.RemovePass(itemId)` + 메시지 트리거
- `ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount) -> CommonResult` — `_storage.AddTreasure(gradeType, amount)`

에러 모델:
- null/empty 입력, 음수 amount, factory lookup 실패는 `CommonResult.Failure(...)`로 반환한다.
- Reward/SaveData 같은 상위 boundary가 이 결과를 받아 원자성/복구 정책을 결정한다.
- public Apply API를 `throw` 중심으로 바꾸지 않는다.

### Revoke

- `RevokeCurrency(CURRENCY_TYPE currencyType, long amount)` — `_storage.Wallet.TryAdd(currencyType, -amount)`
- `RevokeEquip(string itemId, int amount)` — itemId별 인스턴스를 amount만큼 제거
- `RevokeCard(string itemId, int amount)` — `card.AddAmount(-amount)`
- `RevokeMaterial(string itemId, int amount)` — `material.AddAmount(-amount)`
- `RevokeHero(string itemId, int amount)` — `hero.AddAmount(-amount)`
- `RevokeRental(string itemId)` — `_storage.RemoveRental(itemId)`
- `RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount)` — `_storage.SetTreasureCount(gradeType, current - amount)`

### Query

- `GetCurrencyAmount(CURRENCY_TYPE currencyType) -> long`
- `GetEquipCount(string itemId) -> int` — 해당 itemId를 가진 인스턴스 수
- `GetCardAmount(string itemId) -> long` — `AbilityItemCard.Amount`
- `GetMaterialAmount(string itemId) -> long` — `AbilityItemMaterial.Amount`
- `GetHeroAmount(string itemId) -> long` — `hero[STAT_TYPE.ITEM_AMOUNT]`
- `HasActiveRental(string itemId) -> bool`
- `HasPass(string itemId) -> bool`
- `GetTreasureCount(TREASURE_GRADE_TYPE gradeType) -> int`

### Message

- `Subcribe(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Handler handler)`
- `UnSubcribe(EntityId ownerKey)`


---


## Internal State (설계)

```csharp
// InventoryManager 내부
readonly InventoryStorage _storage = new();
```

- 통화 상태는 `_storage.Wallet[currencyId]` → `long`으로 관리한다.
- 장비 상태는 `_storage.Equipments[itemUid]` → `AbilityItemEquip`이 전담한다.
- 카드 상태는 `_storage.Cards[itemId]` → `AbilityItemCard`가 전담한다.
- 재료 상태는 `_storage.Materials[itemId]` → `AbilityItemMaterial`이 전담한다.
- 영웅 상태는 `_storage.Heroes[itemId]` → `AbilityItemHero`가 전담한다.
- 렌탈 상태는 `_storage.Rentals[itemId]` → `long`(expiresAtClientUtcMs)으로 관리한다.
- 시즌패스 상태는 `_storage.Passes[itemId]` → `bool`(owned)으로 관리한다.
- treasure count 상태는 `_storage.TreasureCounts[gradeType]` → `int`(보유량)로 관리한다.
- treasure current 상태는 `_storage.TreasureCurrent` → `InventoryTreasureCurrent`(Exp/Level)로 관리한다.

NOTE:
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `itemId`에 여러 인스턴스가 존재할 수 있다.
- 카드 수량 SSOT = `AbilityItemCard.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- 재료 수량 SSOT = `AbilityItemMaterial.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- 영웅 수량 SSOT = `AbilityItemHero.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- `AbilityItemEquip`이 능력치를 관리한다. outgame 장비 장착은 `AbilityItemHero`가 담당한다.
- ability 인스턴스 생성은 [15-game-ability-factory](../../../21-game-package/15-game-ability-factory/SKILL.md)의 `AbilityItemFactory`를 우선 사용한다.
- `COMMON_ERROR`는 append-only SSOT다. inventory 에러 코드는 새 row append로만 추가한다.


---


## asmdef

`Devian.Samples.MobilePackage.asmdef`에 포함된 참조:
- `Devian.Domain.Common` — `CommonResult`, `CommonError`, `COMMON_ERROR_TYPE`
- `Devian.Domain.Game` — `STAT_TYPE` (AbilityItemEquip → AbilityBase 경유, InventoryStorage 의존)


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Inventory/InventoryManager.cs`


---


## Notes

- 내부 구현 메서드는 Devian 정책에 따라 `_MethodName` 네이밍을 사용한다(구현 단계).
- `InventoryMessageTrigger`는 `InventoryManager` 내부 소유 객체이며 외부 직접 노출 금지다.


---


## Related

- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage (소유 대상)
- [12-inventory-wallet](../12-inventory-wallet/SKILL.md) — InventoryWallet (Wallet 클래스)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md) — RewardManager (RewardData 해석, ApplyRewardDatas)
- [49-reward-system/12-first-reward-settings](../../49-reward-system/12-first-reward-settings/SKILL.md) — FirstRewardSettings (초기 보상 지급 설정)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md) — RewardData 스키마 정본
- [03-ssot](../03-ssot/SKILL.md) — Inventory 상태/Apply 규칙 SSOT
- [01-policy](../01-policy/SKILL.md) — Inventory 하드룰
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 정본
- [15-singleton](../../../20-common-package/29-singleton/SKILL.md) — CompoSingleton 규약
