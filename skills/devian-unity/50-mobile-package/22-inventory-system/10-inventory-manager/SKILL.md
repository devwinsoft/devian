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
    public void ApplyCurrency(CURRENCY_TYPE currencyType, long amount) { ... }
    public void ApplyEquip(string equipId, int amount) { ... }
    public void ApplyCard(string cardId, int amount) { ... }
    public void ApplyHero(string heroId, int amount) { ... }
    public void ApplyRental(string rentalId) { ... }
    public void ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }
    public void SetPassOwnership(string passId, bool owned) { ... }
    public void RemovePassOwnership(string passId) { ... }

    // ── Revoke API ──
    public void RevokeCurrency(CURRENCY_TYPE currencyType, long amount) { ... }
    public void RevokeEquip(string equipId, int amount) { ... }
    public void RevokeCard(string cardId, int amount) { ... }
    public void RevokeHero(string heroId, int amount) { ... }
    public void RevokeRental(string rentalId) { ... }
    public void RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }

    // ── Query API ──
    public long GetCurrencyAmount(CURRENCY_TYPE currencyType) { ... }
    public int GetEquipCount(string equipId) { ... }
    public long GetCardAmount(string cardId) { ... }
    public long GetHeroAmount(string heroId) { ... }
    public bool HasActiveRental(string rentalId) { ... }
    public bool HasPass(string passId) { ... }
    public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType) { ... }

    // ── Message ──
    public void Subcribe(EntityId ownerKey, MESSAGE_INVENTORY_TYPE msgType, Handler handler) { ... }
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
  - `ApplyEquip`: 새 `itemUid`(GUID)로 AbilityEquip 인스턴스 생성, amount 횟수만큼
  - `ApplyCard`: AbilityCard 추가/갱신 (`STAT_TYPE.CARD_AMOUNT` 누적)
  - `ApplyHero`: AbilityUnitHero 추가/갱신 (`STAT_TYPE.UNIT_AMOUNT` 누적)
  - `ApplyRental`: 로컬 만료 시각 설정/연장 (`max(currentExpiry, now) + 30days`)
  - `SetPassOwnership`: 소유권 설정 + 메시지 트리거
  - `ApplyTreasure`: chest count 누적
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

- `ApplyCurrency(CURRENCY_TYPE currencyType, long amount)` — `_storage.Wallet.TryAdd(currencyType, amount)`
- `ApplyEquip(string equipId, int amount)` — amount 횟수만큼 새 `itemUid`(GUID)로 AbilityEquip 생성 + `_storage.AddEquip(itemUid, ability)`
- `ApplyCard(string cardId, int amount)` — `_storage.Cards`에 AbilityCard 추가(없으면 생성) + `AbilityCard.AddAmount(amount)`
- `ApplyHero(string heroId, int amount)` — `_storage.Heroes`에 AbilityUnitHero 추가(없으면 생성) + `AddStat(STAT_TYPE.UNIT_AMOUNT, amount)`
- `ApplyRental(string rentalId)` — `_storage.SetRental(id, max(currentExpiry, now)+30days)`
- `SetPassOwnership(string passId, bool owned)` — `_storage.SetPass(passId, owned)` + 메시지 트리거
- `RemovePassOwnership(string passId)` — `_storage.RemovePass(passId)` + 메시지 트리거
- `ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount)` — `_storage.AddTreasure(gradeType, amount)`

### Revoke

- `RevokeCurrency(CURRENCY_TYPE currencyType, long amount)` — `_storage.Wallet.TryAdd(currencyType, -amount)`
- `RevokeEquip(string equipId, int amount)` — equipId별 인스턴스를 amount만큼 제거
- `RevokeCard(string cardId, int amount)` — `card.AddAmount(-amount)`
- `RevokeHero(string heroId, int amount)` — `hero.AddStat(STAT_TYPE.UNIT_AMOUNT, -amount)`
- `RevokeRental(string rentalId)` — `_storage.RemoveRental(rentalId)`
- `RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount)` — `_storage.SetTreasureCount(gradeType, current - amount)`

### Query

- `GetCurrencyAmount(CURRENCY_TYPE currencyType) -> long`
- `GetEquipCount(string equipId) -> int` — 해당 equipId를 가진 인스턴스 수
- `GetCardAmount(string cardId) -> long` — `AbilityCard.Amount`
- `GetHeroAmount(string heroId) -> long` — `hero[STAT_TYPE.UNIT_AMOUNT]`
- `HasActiveRental(string rentalId) -> bool`
- `HasPass(string passId) -> bool`
- `GetTreasureCount(TREASURE_GRADE_TYPE gradeType) -> int`

### Message

- `Subcribe(EntityId ownerKey, MESSAGE_INVENTORY_TYPE msgType, Handler handler)`
- `UnSubcribe(EntityId ownerKey)`


---


## Internal State (설계)

```csharp
// InventoryManager 내부
readonly InventoryStorage _storage = new();
```

- 통화 상태는 `_storage.Wallet[currencyId]` → `long`으로 관리한다.
- 장비 상태는 `_storage.Equipments[itemUid]` → `AbilityEquip`이 전담한다.
- 카드 상태는 `_storage.Cards[cardId]` → `AbilityCard`가 전담한다.
- 영웅 상태는 `_storage.Heroes[heroId]` → `AbilityUnitHero`가 전담한다.
- 렌탈 상태는 `_storage.Rentals[rentalTypeId]` → `long`(expiresAtClientUtcMs)으로 관리한다.
- 시즌패스 상태는 `_storage.Passes[passId]` → `bool`(owned)으로 관리한다.
- treasure count 상태는 `_storage.TreasureCounts[gradeType]` → `int`(보유량)로 관리한다.
- treasure current 상태는 `_storage.TreasureCurrent` → `InventoryTreasureCurrent`(Exp/Level)로 관리한다.

NOTE:
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- 카드 수량 SSOT = `AbilityCard.Amount` (= `this[STAT_TYPE.CARD_AMOUNT]`).
- 영웅 수량 = `AbilityUnitHero[STAT_TYPE.UNIT_AMOUNT]`.
- `AbilityEquip`이 능력치를 관리한다. 장비 장착은 `AbilityUnitHero`가 담당한다.


---


## asmdef

`Devian.Samples.MobilePackage.asmdef`에 포함된 참조:
- `Devian.Domain.Common` — `CommonResult`, `CommonError`, `COMMON_ERROR_TYPE`
- `Devian.Domain.Game` — `STAT_TYPE` (AbilityEquip → AbilityBase 경유, InventoryStorage 의존)


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
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
