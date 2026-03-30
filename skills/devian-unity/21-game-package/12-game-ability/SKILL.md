# 12-game-ability

Status: ACTIVE
AppliesTo: v10

**GamePackage Ability addon.**
`devian/21-domain-game`에서 생성된 `STAT_TYPE`, `ITEM_*`, `UNIT_*` 타입 위에 얹히는 Unity 수동 C# 계층을 정의한다.

---

## 1. Scope

- `AbilityBase` — `Dictionary<STAT_TYPE, int>` 기반 정규화 stat 저장소
- `AbilityItemBase` / `AbilityItemEquip` / `AbilityItemCard` / `AbilityItemMaterial` / `AbilityItemHero` — outgame item runtime 모델
- `AbilityUnitBase` / `AbilityUnitHero` / `AbilityUnitMonster` — unit runtime 모델
- Generated enum/table 정의 자체는 [devian/21-domain-game](../../../devian/21-domain-game/00-overview/SKILL.md)가 정본이다

---

## 2. Code Location

### UPM (정본)

- `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/`

### Packages (sync)

- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/GamePackage/Runtime/Ability/`

### Assets/Samples (import)

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{foundationVersion}/GamePackage/Runtime/Ability/`

---

## 3. Files

| 파일 | 역할 |
|---|---|
| `AbilityBase.cs` | stat dictionary, indexer, `AddStat`, `SetStat`, `ClearStat`, `Clone` 공통 베이스 |
| `AbilityItemBase.cs` | item 공통 abstract 베이스 (`Amount`, `Level`, `AddAmount`) |
| `AbilityItemEquip.cs` | `ITEM_EQUIP` 참조 + `ItemUid` + owner/unit slot 상태 |
| `AbilityItemCard.cs` | `ITEM_CARD` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 |
| `AbilityItemMaterial.cs` | `ITEM_MATERIAL` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 |
| `AbilityItemHero.cs` | `ITEM_HERO` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 + outgame equip slot 소유 |
| `AbilityUnitBase.cs` | unit 공통 abstract 베이스 (`UnitId`) |
| `AbilityUnitHero.cs` | `UNIT_HERO` 초기화 + preview용 projected equip snapshot |
| `AbilityUnitMonster.cs` | `UNIT_MONSTER` 초기화 + 기본 stat 세팅 |

---

## 4. Generated Dependencies

### Consumed Generated Types

- `Devian.Domain.Game.STAT_TYPE`
- `Devian.Domain.Game.ITEM_EQUIP`
- `Devian.Domain.Game.ITEM_CARD`
- `Devian.Domain.Game.ITEM_MATERIAL`
- `Devian.Domain.Game.ITEM_HERO`
- `Devian.Domain.Game.UNIT_HERO`
- `Devian.Domain.Game.UNIT_MONSTER`

### Assembly

`Devian.Samples.GamePackage` asmdef는 다음을 참조한다:

- `Devian.Core`
- `Devian.Samples.CommonPackage`
- `Devian.Samples.SoundPackage`

수동 addon 클래스 namespace는 `Devian`이고, generated 타입 namespace는 `Devian.Domain.Game`를 유지한다.

---

## 5. Behavioral Rules

- `AbilityBase`는 `(STAT_TYPE, int)` 정규화 저장소를 단일 SSOT로 사용한다.
- `AbilityItemEquip`은 `itemUid`를 인스턴스 pk로 사용하고, 장착 상태는 `OwnerUnitId` + `OwnerSlotNumber`로 관리한다.
- `AbilityItemCard.Amount`, `AbilityItemMaterial.Amount`, `AbilityItemHero.Amount`는 `AbilityItemBase.Amount`를 상속하며, `STAT_TYPE.ITEM_AMOUNT`의 얇은 래퍼다.
- `AbilityItemHero`가 outgame hero equip ownership의 실제 변경 지점을 가진다. `Equips` / `Equip(equip, slot)` / `Unequip(slot)` 메서드를 제공한다.
- `AbilityUnitHero`는 `UNIT_HERO` 기반 unit stat 모델이며, preview/ingame projection용 `Equips` snapshot만 유지한다. 직접 소유/장착 규칙 정본은 [15-game-ability-factory](../15-game-ability-factory/SKILL.md)다.
- `AbilityUnitHero.Init()` / `AbilityUnitMonster.Init()`는 `STAT_TYPE.UNIT_HP_MAX`를 `table.MaxHp`로 세팅한다.
- clone 동작은 generated table 참조는 유지하고 stat 값만 복사한다.

---

## 6. Hard Rules

- 수동 addon 코드는 `Runtime/Ability/`에만 둔다.
- `Runtime/Generated/`, `Editor/Generated/`는 빌더 관리 영역이다. 수동 수정 금지.
- `STAT_TYPE`, `ITEM_*`, `UNIT_*`는 generated 타입이다. 수동 재정의 금지.
- 정본 수정 위치는 항상 `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/`다.
- 장착/해제 규칙을 바꾸면 Inventory 계열 스킬 문서도 함께 갱신한다.

---

## 7. Related

- [devian/21-domain-game/12-game-ability](../../../devian/21-domain-game/12-game-ability/SKILL.md) — Ability feature 모델/TS 관점
- [devian/21-domain-game/13-game-stat-type](../../../devian/21-domain-game/13-game-stat-type/SKILL.md) — `STAT_TYPE` 정의
- [15-game-ability-factory](../15-game-ability-factory/SKILL.md) — ability 생성 / projection / preview 규약
- [devian-unity/50-mobile-package/22-inventory-system/03-ssot](../../50-mobile-package/22-inventory-system/03-ssot/SKILL.md) — Inventory 상태/Apply 규약
- [devian-unity/50-mobile-package/22-inventory-system/11-inventory-storage](../../50-mobile-package/22-inventory-system/11-inventory-storage/SKILL.md) — Ability 저장/장착 위임
