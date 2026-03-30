# 15-game-ability-factory

Status: ACTIVE
AppliesTo: v10

**GamePackage Ability factory layer.**
`AbilityItem*`, `AbilityUnit*` 생성 로직을 한 곳에 모아 `table lookup -> new -> Init -> stat/equip projection` 흐름을 표준화한다.

---

## 1. Scope

- `AbilityItemFactory` — `ITEM_*` 기반 outgame ability 생성
- `AbilityUnitFactory` — `UNIT_*` 기반 ingame ability 생성
- `AbilityUnitHeroContext` — outgame preview / simulate 용 hero projection context
- `AbilityEquipProjection` 또는 동등 개념 — equip clone/slot projection 규약

---

## 2. Why This Exists

현재 생성 로직은 다음처럼 여러 곳에 흩어지기 쉽다.

- `TB_ITEM_*` / `TB_UNIT_*` 조회
- `new Ability...()`
- `Init(table)`
- stat 복사
- equip owner/slot 정리

이 패턴이 Inventory, SaveData 복원, preview, battle spawn에 반복되면 다음 문제가 생긴다.

- 생성 규칙이 분산됨
- preview가 inventory 인스턴스를 직접 오염시킬 위험이 생김
- `AbilityItem* -> AbilityUnit*` 변환 규칙이 호출부마다 달라짐
- `ITEM_*`와 `UNIT_*`를 암묵적으로 연결하려는 잘못된 설계가 섞이기 쉬움

Factory layer의 목적은 **생성 책임을 중앙화**하고, 특히 **outgame preview용 projection**을 명시적인 context로 다루는 것이다.
예상 가능한 validation / lookup 실패는 `throw`가 아니라 `CommonResult.Failure(...)`로 반환한다.

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
| `AbilityFactoryStatUtil.cs` | stat copy / override / whitelist 적용 헬퍼 |

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
- ability 인스턴스 생성
- `Init(...)`
- 저장된 stat 적용
- 필요 시 equip/owner 상태 복원

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
    SourceItemHero = itemHero,
    SourceEquips = itemHero.Equips,
    CopyItemLevel = true,
    CloneEquips = true,
};

var previewResult = AbilityUnitFactory.CreateHero(ctx);
if (previewResult.IsFailure)
    return previewResult;

var preview = previewResult.Value;
```

그리고 convenience API는 내부적으로 context로 변환한다.

```csharp
var previewResult = AbilityUnitFactory.CreateHero(itemHero, unitId);
```

---

## 5. AbilityUnitHeroContext

`AbilityUnitHeroContext`는 **projection 입력 데이터**다.
핵심은 `AbilityUnitHero`가 item world와 직접 결합하지 않도록 중간층을 두는 것이다.

### Required

- `string UnitId`

### Optional

- `AbilityItemHero SourceItemHero`
- `IReadOnlyDictionary<int, AbilityItemEquip> SourceEquips`
- `IReadOnlyDictionary<STAT_TYPE, int> OverrideStats`
- `bool CopyItemLevel`
- `bool CloneEquips`

### Notes

- `UnitId`는 필수다.
- `SourceItemHero`는 선택이다.
- `SourceItemHero`가 있어도 `UNIT_HERO`를 자동 추론하지 않는다.
- `OverrideStats`는 preview/buff/test 전용 덮어쓰기 입력이다.
- invalid input / lookup failure는 `CommonResult.Failure(...)`로 반환한다.

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

## 7. Equip Projection Rule

Preview/simulate에서 가장 위험한 부분은 equip 참조 공유다.

잘못된 예:

- inventory의 `AbilityItemEquip`를 그대로 `AbilityUnitHero`에 장착
- preview에서 `SetOwner/ClearOwner`가 원본 inventory 상태를 오염

권장 규칙:

- 기본값은 `CloneEquips = true`
- preview unit에는 **equip clone**을 장착한다
- clone 후 owner를 preview unit 기준으로 다시 세팅한다

예시 흐름:

1. source equip clone
2. clone owner clear
3. preview unit에 장착
4. slot owner 재설정

---

## 8. Recommended API Shape

```csharp
using Devian.Domain.Common;

public static class AbilityItemFactory
{
    public static CommonResult<AbilityItemCard> CreateCard(ITEM_CARD table,
        IReadOnlyDictionary<STAT_TYPE, int> stats = null);

    public static CommonResult<AbilityItemMaterial> CreateMaterial(ITEM_MATERIAL table,
        IReadOnlyDictionary<STAT_TYPE, int> stats = null);

    public static CommonResult<AbilityItemHero> CreateHero(ITEM_HERO table,
        IReadOnlyDictionary<STAT_TYPE, int> stats = null,
        IReadOnlyDictionary<int, AbilityItemEquip> equips = null,
        bool cloneEquips = false);

    public static CommonResult<AbilityItemEquip> CreateEquip(ITEM_EQUIP table, string itemUid,
        IReadOnlyDictionary<STAT_TYPE, int> stats = null);
}

public sealed class AbilityUnitHeroContext
{
    public string UnitId { get; init; }
    public AbilityItemHero SourceItemHero { get; init; }
    public IReadOnlyDictionary<int, AbilityItemEquip> SourceEquips { get; init; }
    public IReadOnlyDictionary<STAT_TYPE, int> OverrideStats { get; init; }
    public bool CopyItemLevel { get; init; }
    public bool CloneEquips { get; init; } = true;
}

public static class AbilityUnitFactory
{
    public static CommonResult<AbilityUnitHero> CreateHero(string unitId,
        IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null);

    public static CommonResult<AbilityUnitHero> CreateHero(AbilityUnitHeroContext context);

    public static CommonResult<AbilityUnitHero> CreateHero(AbilityItemHero itemHero, string unitId,
        IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null,
        bool cloneEquips = true);

    public static CommonResult<AbilityUnitMonster> CreateMonster(UNIT_MONSTER table,
        IReadOnlyDictionary<STAT_TYPE, int> overrideStats = null);
}
```

핵심은 overload가 많아 보여도 내부 구현은 `Context -> Create` 한 경로로 수렴해야 한다는 점이다.

---

## 9. Projection Rule for Stats

`AbilityItem*`의 stat을 `AbilityUnit*`에 무조건 복사하면 안 된다.

권장 규칙:

- `ITEM_AMOUNT`는 unit stat으로 복사 금지
- `ITEM_LEVEL`은 preview 계산 입력으로만 사용하고, 필요할 때만 unit stat에 반영
- `UNIT_HP_MAX` 같은 unit 고유 stat은 `UNIT_*` table 또는 명시적 계산식이 정본
- item-derived bonus는 `OverrideStats` 또는 별도 projection 단계에서 더한다

즉 item stat은 **source input**이고, unit stat은 **projection result**다.

---

## 10. Responsibility Split

### Ability class

- 상태 보유
- 최소한의 `Init`, `Clone`, `Equip`, `Unequip`

### Factory

- 생성 절차 조립
- table lookup overload 제공 가능
- stat/equip projection
- preview-safe clone 처리

### Caller

- 어떤 row를 쓸지 결정
- preview인지 ingame spawn인지 결정
- 필요한 resolver/context 제공

---

## 11. Hard Rules

- 새 코드에서는 `new Ability...()` + `Init(...)`를 호출부에 흩뿌리지 말고 factory 경유를 우선한다.
- 새 factory API는 expected failure에서 `throw`하지 않고 `CommonResult`를 반환한다.
- `AbilityUnitHero`가 `ITEM_HERO`를 직접 조회해서 `UNIT_HERO`를 추론하면 안 된다.
- preview factory는 inventory 원본 `AbilityItemEquip` owner 상태를 오염시키면 안 된다.
- direct-reference overload를 만들더라도 내부 정본 경로는 context 기반이어야 한다.
- `AbilityItem*` 생성과 `AbilityUnit*` 생성 규칙은 분리하되, 필요하면 façade `AbilityFactory`로 묶을 수 있다.

---

## 12. Example Flows

### Outgame inventory load

`ITEM_HERO` row + saved stats + saved equips
→ `AbilityItemFactory.CreateHero(...)`
→ `CommonResult<AbilityItemHero>`
→ `IsSuccess` 확인 후 `AbilityItemHero`

### Ingame spawn

`unitId`
→ `AbilityUnitFactory.CreateHero(unitId)`
→ `CommonResult<AbilityUnitHero>`
→ `IsSuccess` 확인 후 `AbilityUnitHero`

### Outgame preview

`AbilityItemHero` + explicit `unitId` + optional override stats
→ `AbilityUnitHeroContext`
→ `AbilityUnitFactory.CreateHero(context)`
→ `CommonResult<AbilityUnitHero>`
→ preview `AbilityUnitHero`

---

## 13. Related

- [12-game-ability](../12-game-ability/SKILL.md) — ability class 계층 정본
- [devian/21-domain-game/12-game-ability](../../../devian/21-domain-game/12-game-ability/SKILL.md) — TS/domain 관점 ability 모델
- [devian-unity/50-mobile-package/22-inventory-system/03-ssot](../../50-mobile-package/22-inventory-system/03-ssot/SKILL.md) — outgame item ability 소비처
