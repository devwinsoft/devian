# 15-game-ability-factory

Status: ACTIVE
AppliesTo: v10

**GamePackage Ability factory layer.**
`AbilityItem*`, `AbilityUnit*` 생성 로직을 한 곳에 모아 `table lookup -> new -> Init` 흐름을 표준화한다.

---

## 1. Scope

- `AbilityItemFactory` — `ITEM_*` 기반 outgame ability 생성
- `AbilityUnitFactory` — `UNIT_*` 기반 ingame ability 생성
- `AbilityUnitHeroContext` — outgame preview / simulate 용 hero projection context

---

## 2. Why This Exists

현재 생성 로직은 다음처럼 여러 곳에 흩어지기 쉽다.

- `TB_ITEM_*` / `TB_UNIT_*` 조회
- `new Ability...()`
- `Init(table, levelTable)`
- stat 추가 적용

이 패턴이 Inventory, SaveData 복원, preview, battle spawn에 반복되면 다음 문제가 생긴다.

- 생성 규칙이 분산됨
- `AbilityItem* -> AbilityUnit*` 변환 규칙이 호출부마다 달라짐
- `ITEM_*`와 `UNIT_*`를 암묵적으로 연결하려는 잘못된 설계가 섞이기 쉬움

Factory layer의 목적은 **생성 책임을 중앙화**하고, 특히 **outgame preview용 projection**을 명시적인 context로 다루는 것이다.
예상 가능한 validation / lookup 실패는 `throw`가 아니라 `GameResult.Failure(...)`로 반환한다.

---

## 3. Recommended Structure

### Code Location

- 정본: `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/Factory/`
- sync: `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/Factory/`
- import: `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{foundationVersion}/GamePackage/Runtime/Ability/Factory/`

### Recommended Files

| 파일 | 역할 |
|---|---|
| `AbilityItemFactory.cs` | `AbilityItemCard`, `AbilityItemMaterial`, `AbilityItemHero`, `AbilityItemEquip` 생성 |
| `AbilityUnitFactory.cs` | `AbilityUnitHero`, `AbilityUnitMonster` 생성 |
| `AbilityUnitHeroContext.cs` | hero preview/init 입력 컨텍스트 |
| `AbilityFactoryStatUtil.cs` | item factory stat 적용 헬퍼 |

---

## 4. Design Principle

### 4.1 Item 생성은 table-first

`AbilityItem*`는 generated `ITEM_*` row가 정본이다.

- `AbilityItemCard` ← `ITEM_CARD`
- `AbilityItemMaterial` ← `ITEM_MATERIAL`
- `AbilityItemHero` ← `ITEM_HERO`
- `AbilityItemEquip` ← `ITEM_EQUIP` + `itemUid`

Item factory는 다음 책임만 가진다.

- row 검증
- target `item_level` 검증
- 대응 `ITEM_*_LEVEL` row 조회
- ability 인스턴스 생성
- `Init(table, levelTable, ...)`
- equip item 생성 시 owner 상태 복원

level table 규칙:

- 대상 level은 factory 인자 `item_level`이고, 기본값은 `1`
- item factory는 level row stat을 직접 apply 하지 않는다
- `AbilityItem*.Init(table, levelTable, ...)`가 level row stat을 초기 적용한다
- stack amount 같은 mutable 상태 복원은 factory 바깥 호출부가 담당한다
- hero equip loadout 저장은 `AbilityItemHero._SetEquip()/_RemoveEquip()` 또는 inventory facade가 담당한다
- 즉 순서는 `find level row -> Init(table, levelTable, ...) -> restore mutable state` 다
- level row가 없으면 `GameResult.Failure(GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND, ...)`
- 빈 stat slot(`UNIT_STAT_TYPE.NONE`)은 skip한다
- level up runtime 갱신은 factory의 next-level resolve helper를 사용한다 (`ResolveNextCardLevelTable`, `ResolveNextHeroLevelTable`, `ResolveNextEquipLevelTable`).
- equip 생성 시 owner 복원 인자는 `EQUIP_SLOT_TYPE ownerSlotType`를 사용하고 `NONE`은 미장착을 의미한다.
- `ITEM_EQUIP`는 `equip_type`를 가지며 slot 허용/양손 여부는 `TB_EQUIP_SLOT.Get(equip_type)`로 resolve한다.

### 4.2 Unit 생성은 unit-table-first

`AbilityUnit*`는 ingame 모델이므로 생성의 기준은 항상 `UNIT_*`다.

- `AbilityUnitHero` ← `UNIT_HERO`
- `AbilityUnitMonster` ← `UNIT_MONSTER`

즉 hero preview를 하더라도 unit 능력치 초기화의 기준은 `UNIT_HERO`여야 한다.

### 4.3 Preview는 context-first

`AbilityUnitHero`가 outgame item 상태를 참고해야 하는 경우가 있다.
이때 기본 API는 `AbilityItemHero` 직접 참조보다 **명시적 context**를 우선한다.

권장 순서:

1. `AbilityUnitHeroContext`
2. `AbilityItemHero`를 받는 convenience overload

즉, 아래가 권장된다.

```csharp
var ctx = new AbilityUnitHeroContext
{
    UnitId = unitId,
    UnitLevel = itemHero.ItemLevel,
    Equips = itemHero.Equips,
};

var previewResult = AbilityUnitFactory.CreateHero(ctx);
if (previewResult.IsFailure)
    return previewResult;

var preview = previewResult.Value;
```

그리고 convenience API는 내부적으로 context로 변환한다.

```csharp
var previewResult = AbilityUnitFactory.CreateHero(itemHero);
```

---

## 5. AbilityUnitHeroContext

`AbilityUnitHeroContext`는 **projection 입력 데이터**다.
핵심은 `AbilityUnitHero`가 item world와 직접 결합하지 않도록 중간층을 두는 것이다.

### Required

- `string UnitId`

### Optional

- `int UnitLevel`
- `IReadOnlyDictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> Equips`

### Notes

- `UnitId`는 필수다.
- `UnitLevel` 기본값은 `1`이다.
- `Equips`는 preview/unit 계산에 사용할 장비 입력이다. factory는 clone 후 `AbilityUnitHero._Equip()`으로 projection한다.
- projection 시 slot validation과 two-handed 규칙은 `AbilityEquipSlotPolicy`를 공유한다.
- invalid input / lookup failure는 `GameResult.Failure(...)`로 반환한다.

---

## 6. No Implicit Mapping Rule

**`ITEM_HERO`와 `UNIT_HERO` 사이의 암묵 규약을 만들지 않는다.**

금지:

- `itemHero.ItemId == unitHero.UnitId` 가정
- 문자열 prefix/suffix 변환으로 `UNIT_HERO` 추론
- factory 내부에서 임의 매핑 규칙 하드코딩

허용:

- 호출부가 `unitId`를 명시적으로 제공
- 별도 link table / config / resolver가 생긴 뒤, 그 resolver를 factory에 주입

즉 preview를 위해 `AbilityItemHero`를 unit로 바꾸려면 반드시 다음 둘 중 하나가 필요하다.

- `unitId` 직접 전달
- 명시적 resolver 전달

---

## 7. Current Preview Rule

현재 지원 범위에서 preview 입력은 `UnitId`, `UnitLevel`, `Equips`를 사용한다.

- `Equips`는 clone 후 `AbilityUnitHero._Equip()`으로 투영된다.
- slot key는 `EQUIP_SLOT_TYPE`이고 `NONE`은 유효한 projection slot이 아니다.
- item hero는 저장 모델이고, unit hero가 계산 모델이다.

---

## 8. Recommended API Shape

```csharp
using Devian.Domain.Game;

public static class AbilityItemFactory
{
    public static GameResult<AbilityItemCard> CreateCard(ITEM_CARD table,
        int itemLevel = 1);

    public static GameResult<AbilityItemMaterial> CreateMaterial(ITEM_MATERIAL table);

    public static GameResult<AbilityItemHero> CreateHero(ITEM_HERO table,
        int itemLevel = 1);

    public static GameResult<AbilityItemEquip> CreateEquip(ITEM_EQUIP table, string itemUid,
        int itemLevel = 1, EQUIP_SLOT_TYPE ownerSlotType = EQUIP_SLOT_TYPE.NONE);
}

public sealed class AbilityItemCard
{
    public void Init(ITEM_CARD table, ITEM_CARD_LEVEL levelTable);
}

public sealed class AbilityItemHero
{
    public void Init(ITEM_HERO table, ITEM_HERO_LEVEL levelTable);
}

public sealed class AbilityItemEquip
{
    public void Init(ITEM_EQUIP table, ITEM_EQUIP_LEVEL levelTable, string itemUid);
}

public sealed class AbilityUnitHeroContext
{
    public string UnitId { get; init; }
    public int UnitLevel { get; init; } = 1;
    public IReadOnlyDictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> Equips { get; init; }
}

public static class AbilityUnitFactory
{
    public static GameResult<AbilityUnitHero> CreateHero(string unitId,
        int unitLevel = 1);

    public static GameResult<AbilityUnitHero> CreateHero(UNIT_HERO table,
        int unitLevel = 1);

    public static GameResult<AbilityUnitHero> CreateHero(AbilityUnitHeroContext context);

    public static GameResult<AbilityUnitHero> CreateHero(AbilityItemHero itemHero);

    public static GameResult<AbilityUnitMonster> CreateMonster(UNIT_MONSTER table,
        int unitLevel = 1);
}
```

핵심은 overload가 많아 보여도 내부 구현은 `Context -> Create` 한 경로로 수렴해야 한다는 점이다.

---

## 9. Projection Rule for Stats

`AbilityUnitFactory.CreateHero(context)`는 `UNIT_*_LEVEL`로 초기화한 뒤 `context.Equips`를 `AbilityUnitHero._Equip()`으로 계산한다.

권장 규칙:

- unit 생성 시 선택된 level row는 `UNIT_LEVEL`과 `UNIT_HP`를 정본으로 공급한다
- `UNIT_HP` 같은 unit 고유 stat은 `UNIT_*_LEVEL` table이 정본이다
- 현재 공식 SSOT에 없는 stat 합성 규칙은 factory에 임의로 하드코딩하지 않는다

---

## 10. Responsibility Split

### Ability class

- 상태 보유
- 최소한의 `Init`, `Clone`, `Equip`, `Unequip`

### Factory

- 생성 절차 조립
- table lookup overload 제공 가능
- context 검증
- 추가 stat 적용

### Caller

- 어떤 row를 쓸지 결정
- preview인지 ingame spawn인지 결정
- 필요한 resolver/context 제공

---

## 11. Hard Rules

- 새 코드에서는 `new Ability...()` + `Init(...)`를 호출부에 흩뿌리지 말고 factory 경유를 우선한다.
- 새 factory API는 expected failure에서 `throw`하지 않고 `GameResult`를 반환한다.
- `AbilityUnitHero`가 `ITEM_HERO`를 직접 조회해서 `UNIT_HERO`를 추론하면 안 된다.
- direct-reference overload를 만들더라도 내부 정본 경로는 context 기반이어야 한다.
- `AbilityItem*` 생성과 `AbilityUnit*` 생성 규칙은 분리하되, 필요하면 façade `AbilityFactory`로 묶을 수 있다.

---

## 12. Example Flows

### Outgame inventory load

`ITEM_HERO` row + saved level/amount/equips
→ `AbilityItemFactory.CreateHero(...)`
→ `GameResult<AbilityItemHero>`
→ `IsSuccess` 확인 후 `AbilityItemHero`

### Ingame spawn

`unit_id`
→ `AbilityUnitFactory.CreateHero(unit_id)`
→ `GameResult<AbilityUnitHero>`
→ `IsSuccess` 확인 후 `AbilityUnitHero`

### Outgame preview

`AbilityItemHero` + explicit `unit_id` + optional override stats
→ `AbilityUnitHeroContext`
→ `AbilityUnitFactory.CreateHero(context)`
→ `GameResult<AbilityUnitHero>`
→ preview `AbilityUnitHero`

---

## 13. Related

- [12-game-ability](../12-game-ability/SKILL.md) — ability class 계층 정본
- [devian/21-domain-game/12-game-ability](../../../devian/21-domain-game/12-game-ability/SKILL.md) — TS/domain 관점 ability 모델
- [devian-unity/50-mobile-package/22-inventory-system/03-ssot](../../50-mobile-package/22-inventory-system/03-ssot/SKILL.md) — outgame item ability 소비처
