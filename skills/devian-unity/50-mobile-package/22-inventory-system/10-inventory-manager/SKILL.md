# 10-inventory-manager


Status: ACTIVE
AppliesTo: v10


InventoryManager(구현 규약)는 인벤토리 상태를 관리하는 시스템이다. 타입별 구체 API를 제공한다.

- `RewardData` 해석(type switch)은 InventoryManager의 책임이 아니다 → [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)가 담당.
- InventoryManager는 `RewardData`를 직접 참조하지 않는다.

InventoryManager는 **단일 concrete 클래스**이다.
live `InventoryStorage`를 private state로 소유하며, 외부에는 query/mutation/message boundary만 노출한다.
save/load는 live storage를 직접 건드리지 않고 `InventorySnapshot`으로 반입/반출한다.


---


## Class Design

```csharp
using Devian.Domain.Common;

public sealed class InventoryManager : CompoSingleton<InventoryManager>
{
    readonly InventoryStorage _storage = new();
    readonly InventoryMessageTrigger _messageTrigger = new();
    public static InventoryManager Instance
        => CompoSingleton<InventoryManager>.Instance;

    public InventorySnapshot CreateSnapshot() { ... }
    public GameResult ReplaceState(InventorySnapshot snapshot, INVENTORY_SNAPSHOT_CHANGE_REASON reason) { ... }
    public void ClearState(INVENTORY_SNAPSHOT_CHANGE_REASON reason) { ... }

    // ── Apply API ──
    public GameResult ApplyCurrency(CURRENCY_TYPE currency_type, long amount) { ... }
    public GameResult ApplyEquip(string item_id, int amount) { ... }
    public GameResult ApplyCard(string item_id, int amount) { ... }
    public GameResult AddCardAmount(string item_id, int delta) { ... }
    public GameResult ApplyMaterial(string item_id, int amount) { ... }
    public GameResult AddMaterialAmount(string item_id, int delta) { ... }
    public GameResult ApplyHero(string item_id, int amount) { ... }
    public GameResult AddHeroAmount(string item_id, int delta) { ... }
    public GameResult LevelUpCard(string item_id) { ... }
    public GameResult LevelUpHero(string item_id) { ... }
    public GameResult LevelUpEquip(string item_uid) { ... }
    public GameResult SetHeroEquip(string heroId, SLOT_TYPE slotType, string equipUid) { ... }
    public GameResult RemoveHeroEquip(string heroId, SLOT_TYPE slotType) { ... }
    public GameResult ApplyRental(string item_id) { ... }
    public GameResult ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }
    public GameResult SetPassOwnership(string item_id, bool owned) { ... }
    public GameResult RemovePassOwnership(string item_id) { ... }

    // ── Revoke API ──
    public GameResult RevokeCurrency(CURRENCY_TYPE currency_type, long amount) { ... }
    public GameResult RevokeEquip(string item_id, int amount) { ... }
    public GameResult RevokeCard(string item_id, int amount) { ... }
    public GameResult RevokeMaterial(string item_id, int amount) { ... }
    public GameResult RevokeHero(string item_id, int amount) { ... }
    public GameResult RevokeRental(string item_id) { ... }
    public GameResult RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount) { ... }

    // ── Query API ──
    public long GetCurrencyAmount(CURRENCY_TYPE currency_type) { ... }
    public IReadOnlyDictionary<string, AbilityItemEquip> GetEquipments() { ... }
    public AbilityItemEquip GetEquip(string item_uid) { ... }
    public IReadOnlyList<AbilityItemEquip> GetEquipsByItemId(string item_id) { ... }
    public int GetEquipCount(string item_id) { ... }
    public IReadOnlyDictionary<string, AbilityItemCard> GetCards() { ... }
    public AbilityItemCard GetCard(string item_id) { ... }
    public long GetCardAmount(string item_id) { ... }
    public IReadOnlyDictionary<string, AbilityItemMaterial> GetMaterials() { ... }
    public AbilityItemMaterial GetMaterial(string item_id) { ... }
    public long GetMaterialAmount(string item_id) { ... }
    public IReadOnlyDictionary<string, AbilityItemHero> GetHeroes() { ... }
    public AbilityItemHero GetHero(string item_id) { ... }
    public long GetHeroAmount(string item_id) { ... }
    public IReadOnlyDictionary<string, long> GetRentals() { ... }
    public bool HasActiveRental(string item_id) { ... }
    public long GetRentalRemainingMs(string item_id) { ... }
    public IReadOnlyDictionary<string, bool> GetPasses() { ... }
    public bool HasPass(string item_id) { ... }
    public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType) { ... }

    // ── Spend API ──
    public bool HasSufficientCurrency(CURRENCY_TYPE currencyType, int amount) { ... }
    public bool TrySpendCurrency(CURRENCY_TYPE currencyType, int amount, out CurrencySpendReceipt receipt) { ... }
    public void RollbackCurrencySpend(CurrencySpendReceipt receipt) { ... }

    // ── Message ──
    public void Subcribe(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Handler handler) { ... }
    public void SubcribeOnce(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Action<object[]> handler) { ... }
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
  - `AddCardAmount`: 카드 수량 signed delta 변경 (+적용 / -회수) + 메시지 발행
  - `ApplyMaterial`: AbilityItemMaterial 추가/갱신 (`STAT_TYPE.ITEM_AMOUNT` 누적)
  - `AddMaterialAmount`: 재료 수량 signed delta 변경 (+적용 / -회수) + 메시지 발행
  - `ApplyHero`: AbilityItemHero 추가/갱신 (`STAT_TYPE.ITEM_AMOUNT` 누적)
  - `AddHeroAmount`: 영웅 수량 signed delta 변경 (+적용 / -회수) + 메시지 발행
  - `LevelUpCard`: 카드 runtime level row를 다음 단계로 교체하고 `ITEM_CARD_CHANGED`를 발행
  - `LevelUpHero`: 영웅 runtime level row를 다음 단계로 교체하고 `ITEM_HERO_CHANGED`를 발행
  - `LevelUpEquip`: 장비 runtime level row를 다음 단계로 교체하고 `ITEM_EQUIP_CHANGED`를 발행
  - `SetHeroEquip`: slot rule 검증 + 장비 장착/이동 + 양손 규칙 자동 해제 + 관련 notify 발행
  - `RemoveHeroEquip`: hero slot 기준 장비 해제 + 관련 notify 발행
  - `ApplyRental`: 로컬 만료 시각 설정/연장 (`max(currentExpiry, now) + 30days`)
  - `SetPassOwnership`: 소유권 설정 + 메시지 트리거
- `ApplyTreasure`: chest count 누적
- public Apply API는 boundary다. item ability 생성 실패/invalid input은 `GameResult.Failure(...)`로 반환한다.
- internal helper는 이미 검증된 내부 invariant 위반에 한해 제한적으로 예외를 사용할 수 있다.
- 타입별 Revoke API로 회수한다.
- 타입별 Query API로 수량/상태를 조회한다.
- 외부 시스템의 item/pass/rental 순회는 `InventoryManager` helper를 사용한다. `InventoryStorage` 직접 조회/보관은 금지한다.
- save/load boundary에는 `CreateSnapshot`/`ReplaceState`/`ClearState`를 제공한다.
- runtime query는 live runtime을 반환하지만, runtime mutator는 `GamePackage` internal로 봉인한다.
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
- 저장 시스템은 live `InventoryStorage`를 직접 역직렬화하지 않고 `InventorySnapshot`을 통해 반입한다.
- JSON 직렬화 규약은 [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)를 따른다.


---


## Public API (설계)

### Apply

- `ApplyCurrency(CURRENCY_TYPE currency_type, long amount) -> GameResult` — `_storage.TryAddCurrency(currency_type, amount)`
- `ApplyEquip(string item_id, int amount) -> GameResult` — amount 횟수만큼 새 `itemUid`(GUID)로 AbilityItemEquip 생성 + `_storage.AddEquip(itemUid, ability)`
- `ApplyCard(string item_id, int amount) -> GameResult` — `_storage.Cards`에 AbilityItemCard 추가(없으면 생성) + `AbilityItemCard.AddAmount(amount)`
- `AddCardAmount(string item_id, int delta) -> GameResult` — 카드 수량 signed delta boundary. `delta > 0`이면 apply, `delta < 0`이면 revoke 검증 후 차감
- `ApplyMaterial(string item_id, int amount) -> GameResult` — `_storage.Materials`에 AbilityItemMaterial 추가(없으면 생성) + `AbilityItemMaterial.AddAmount(amount)`
- `AddMaterialAmount(string item_id, int delta) -> GameResult` — 재료 수량 signed delta boundary. `delta > 0`이면 apply, `delta < 0`이면 revoke 검증 후 차감
- `ApplyHero(string item_id, int amount) -> GameResult` — `_storage.Heroes`에 AbilityItemHero 추가(없으면 생성) + `AbilityItemHero.AddAmount(amount)`
- `AddHeroAmount(string item_id, int delta) -> GameResult` — 영웅 수량 signed delta boundary. `delta > 0`이면 apply, `delta < 0`이면 revoke 검증 후 차감
- `LevelUpCard(string item_id) -> GameResult` — 현재 카드 runtime의 level stat을 제거하고 다음 `ITEM_CARD_LEVEL` row stat을 적용
- `LevelUpHero(string item_id) -> GameResult` — 현재 영웅 runtime의 level stat을 제거하고 다음 `ITEM_HERO_LEVEL` row stat을 적용
- `LevelUpEquip(string item_uid) -> GameResult` — 현재 장비 runtime의 level stat을 제거하고 다음 `ITEM_EQUIP_LEVEL` row stat을 적용
- `SetHeroEquip(string heroId, SLOT_TYPE slotType, string equipUid) -> GameResult` — `EQUIP_SLOT` 규칙 검증 후 장비 장착. two-handed main 장착 시 기존 `HAND_SUB`는 자동 해제
- `RemoveHeroEquip(string heroId, SLOT_TYPE slotType) -> GameResult` — hero slot 기준 장비 해제
- `ApplyRental(string item_id) -> GameResult` — `_storage.SetRental(id, max(currentExpiry, now)+30days)`
- `SetPassOwnership(string item_id, bool owned) -> GameResult` — `_storage.SetPass(item_id, owned)` + `PASS_OWNERSHIP_CHANGED`
- `RemovePassOwnership(string item_id) -> GameResult` — `_storage.RemovePass(item_id)` + `PASS_OWNERSHIP_CHANGED`
- `ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount) -> GameResult` — `_storage.AddTreasure(gradeType, amount)` + `TREASURE_STATE_CHANGED`
- `HasSufficientCurrency(CURRENCY_TYPE currencyType, int amount) -> bool` — 잔고 충분 여부 확인 (FREE/ADS는 항상 false)
- `TrySpendCurrency(CURRENCY_TYPE currencyType, int amount, out CurrencySpendReceipt receipt) -> bool` — shop 구매용 currency spend + `CURRENCY_CHANGED`
- `RollbackCurrencySpend(CurrencySpendReceipt receipt)` — spend rollback + `CURRENCY_CHANGED`
- `SetTreasureCurrentState(int level, int exp)` — current chest progress 갱신 + `TREASURE_STATE_CHANGED`
- `ClearState(INVENTORY_SNAPSHOT_CHANGE_REASON reason)` — inventory bulk clear + `INVENTORY_SNAPSHOT_CHANGED`
- `CreateSnapshot() -> InventorySnapshot` — live runtime state를 저장 DTO로 투영
- `ReplaceState(InventorySnapshot snapshot, INVENTORY_SNAPSHOT_CHANGE_REASON reason) -> GameResult` — temp snapshot을 validate/build 후 live state에 교체 적용 + `INVENTORY_SNAPSHOT_CHANGED`

에러 모델:
- null/empty 입력, 음수 amount, factory lookup 실패는 `GameResult.Failure(...)`로 반환한다.
- Reward/SaveData 같은 상위 boundary가 이 결과를 받아 원자성/복구 정책을 결정한다.
- public Apply API를 `throw` 중심으로 바꾸지 않는다.
- `HasSufficientCurrency(CURRENCY_TYPE currencyType, int amount) -> bool` — `FREE`/`ADS`는 항상 false, 그 외 `GetCurrencyAmount(currencyType) >= amount`

### Revoke

- `RevokeCurrency(CURRENCY_TYPE currency_type, long amount) -> GameResult` — `_storage.TryAddCurrency(currency_type, -amount)` with insufficient validation
- `RevokeEquip(string item_id, int amount) -> GameResult` — itemId별 인스턴스를 amount만큼 제거
- `RevokeCard(string item_id, int amount) -> GameResult` — `card.AddAmount(-amount)` with insufficient validation
- `RevokeMaterial(string item_id, int amount) -> GameResult` — `material.AddAmount(-amount)` with insufficient validation
- `RevokeHero(string item_id, int amount) -> GameResult` — `hero.AddAmount(-amount)` with insufficient validation
- `RevokeRental(string item_id) -> GameResult` — `_storage.RemoveRental(item_id)`
- `RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount) -> GameResult` — `_storage.SetTreasureCount(gradeType, current - amount)`

### Query

- `GetCurrencyAmount(CURRENCY_TYPE currency_type) -> long`
- `GetEquipments() -> IReadOnlyDictionary<string, AbilityItemEquip>` — live read-only 장비 인스턴스 맵
- `GetEquip(string item_uid) -> AbilityItemEquip` — itemUid 기준 단건 장비 runtime 조회
- `GetEquipsByItemId(string item_id) -> IReadOnlyList<AbilityItemEquip>` — itemId 기준 장비 인스턴스 목록
- `GetEquipCount(string item_id) -> int` — 해당 itemId를 가진 인스턴스 수
- `GetCards() -> IReadOnlyDictionary<string, AbilityItemCard>` — live read-only 카드 맵
- `GetCard(string item_id) -> AbilityItemCard` — itemId 기준 단건 카드 runtime 조회
- `GetCardAmount(string item_id) -> long` — `AbilityItemCard.Amount`
- `GetMaterials() -> IReadOnlyDictionary<string, AbilityItemMaterial>` — live read-only 재료 맵
- `GetMaterial(string item_id) -> AbilityItemMaterial` — itemId 기준 단건 재료 runtime 조회
- `GetMaterialAmount(string item_id) -> long` — `AbilityItemMaterial.Amount`
- `GetHeroes() -> IReadOnlyDictionary<string, AbilityItemHero>` — live read-only 영웅 맵
- `GetHero(string item_id) -> AbilityItemHero` — itemId 기준 단건 영웅 runtime 조회
- `GetHeroAmount(string item_id) -> long` — `hero[STAT_TYPE.ITEM_AMOUNT]`
- `GetRentals() -> IReadOnlyDictionary<string, long>` — live read-only 렌탈 만료 시각 맵
- `HasActiveRental(string item_id) -> bool`
- `GetRentalRemainingMs(string item_id) -> long`
- `GetPasses() -> IReadOnlyDictionary<string, bool>` — live read-only 패스 소유 맵
- `HasPass(string item_id) -> bool`
- `GetTreasureCount(TREASURE_GRADE_TYPE gradeType) -> int`
- `GetTreasureCurrentLevel() -> int`
- `GetTreasureCurrentExp() -> int`
- query가 반환하는 runtime은 live object다. 읽기 전용으로만 사용하고 장기 보관/직접 mutation을 금지한다.
- 외부 코드가 `GetCard()/GetMaterial()/GetHero()` 결과에 직접 `AddAmount()`를 호출하는 패턴은 금지한다.
- 수량 증감이 필요하면 `AddCardAmount/AddMaterialAmount/AddHeroAmount`를 사용한다.

### Message

- `Subcribe(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Handler handler)`
- `SubcribeOnce(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Action<object[]> handler)`
- `UnSubcribe(EntityId ownerKey)`
- apply/revoke 성공 시 `InventoryManager`가 아래 key를 직접 publish한다.
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
  - `PASS_OWNERSHIP_CHANGED`
  - `TREASURE_STATE_CHANGED`
  - `INVENTORY_SNAPSHOT_CHANGED`
- item changed payload는 `key + current runtime`을 사용한다.
- item list changed payload는 `action(ADD/REMOVE) + key + runtimeOrNull`을 사용한다.
- 장착/해제로 inventory membership은 바뀌지 않으므로 `SetHeroEquip/RemoveHeroEquip`는 `ITEM_*_LIST_CHANGED`를 발행하지 않는다.
- bulk load/import/clear는 세부 delta를 replay하지 않고 `INVENTORY_SNAPSHOT_CHANGED`만 발행한다.


---


## Internal State (설계)

```csharp
// InventoryManager 내부
readonly InventoryStorage _storage = new();
InventorySnapshot CreateSnapshot() { ... }
```

- 통화 상태는 `_storage` 내부 `Dictionary<CURRENCY_TYPE, long>`로 직접 관리한다.
- 장비 상태는 `_storage.Equipments[itemUid]` → `AbilityItemEquip`이 전담한다.
- 카드 상태는 `_storage.Cards[item_id]` → `AbilityItemCard`가 전담한다.
- 재료 상태는 `_storage.Materials[item_id]` → `AbilityItemMaterial`이 전담한다.
- 영웅 상태는 `_storage.Heroes[item_id]` → `AbilityItemHero`가 전담한다.
- 렌탈 상태는 `_storage.Rentals[item_id]` → `long`(expiresAtClientUtcMs)으로 관리한다.
- 시즌패스 상태는 `_storage.Passes[item_id]` → `bool`(owned)으로 관리한다.
- treasure count 상태는 `_storage.TreasureCounts[gradeType]` → `int`(보유량)로 관리한다.
- treasure current 상태는 `_storage.TreasureCurrent` → `InventoryTreasureCurrent`(Exp/Level)로 관리한다.
- persistence SSOT는 live `_storage`가 아니라 `InventorySnapshot` DTO다. codec은 snapshot을 읽고 쓴다.

NOTE:
- 장비는 `itemUid`(GUID)를 pk로 관리한다. 같은 `item_id`에 여러 인스턴스가 존재할 수 있다.
- 카드 수량 SSOT = `AbilityItemCard.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- 재료 수량 SSOT = `AbilityItemMaterial.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- 영웅 수량 SSOT = `AbilityItemHero.Amount` (= `this[STAT_TYPE.ITEM_AMOUNT]`).
- 카드/재료/영웅은 amount가 0이 되면 runtime을 storage에서 제거한다.
- 카드/영웅/장비 level SSOT = 각 runtime의 `STAT_TYPE.ITEM_LEVEL` + 대응 `ITEM_*_LEVEL` row 적용 결과다.
- `AbilityItemEquip`이 능력치를 관리한다. outgame 장비 장착은 `AbilityItemHero`가 담당한다.
- ability 인스턴스 생성은 [15-game-ability-factory](../../../21-game-package/15-game-ability-factory/SKILL.md)의 `AbilityItemFactory`를 우선 사용한다.
- `AbilityBase.AddStat/SetStat/Clear*`, `AbilityItemBase.AddAmount`, `AbilityItemEquip.SetOwner/ClearOwner`는 `GamePackage` internal이다.
- `GAME_ERROR_TYPE`의 inventory 관련 에러 코드: `GAME_INVALID_ARGUMENT`, `INVENTORY_REFUND_INSUFFICIENT`.


---


## asmdef

`Devian.Samples.MobilePackage.asmdef`에 포함된 참조:
- `Devian.Domain.Common` — `EntityId`, `CompoSingleton`
- `Devian.Domain.Game` — `GameResult`, `GameResult<T>`, `GAME_ERROR_TYPE`, `STAT_TYPE`, `CURRENCY_TYPE`, `TREASURE_GRADE_TYPE`, `INVENTORY_MESSAGE_TYPE` 등


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../04-package-policy/SKILL.md)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/InventoryManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Inventory/InventoryManager.cs`


---


## Notes

- 내부 구현 메서드는 Devian 정책에 따라 `_MethodName` 네이밍을 사용한다(구현 단계).
- `InventoryMessageTrigger`는 `InventoryManager` 내부 소유 객체이며 외부 직접 노출 금지다.


---


## Related

- [11-inventory-storage](../11-inventory-storage/SKILL.md) — InventoryStorage (소유 대상)
- [12-inventory-wallet](../12-inventory-wallet/SKILL.md) — Currency State Flattening (legacy wallet wrapper 제거)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md) — RewardManager (RewardData 해석, ApplyRewardDatas)
- [49-reward-system/12-first-reward-settings](../../49-reward-system/12-first-reward-settings/SKILL.md) — FirstRewardSettings (초기 보상 지급 설정)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md) — RewardData 스키마 정본
- [03-ssot](../03-ssot/SKILL.md) — Inventory 상태/Apply 규칙 SSOT
- [01-policy](../01-policy/SKILL.md) — Inventory 하드룰
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — SaveData JSON 직렬화 정본
- [15-singleton](../../../20-common-package/29-singleton/SKILL.md) — CompoSingleton 규약
