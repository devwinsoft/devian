# 12-game-ability

Status: ACTIVE
AppliesTo: v10

**GamePackage Ability addon.**
`devian/21-domain-game`에서 생성된 `UNIT_STAT_TYPE`, `ITEM_*`, `UNIT_*` 타입 위에 얹히는 Unity 수동 C# 계층을 정의한다.

---

## 1. Scope

- `AbilityBase` — `Dictionary<UNIT_STAT_TYPE, int>` 기반 정규화 stat 저장소
- `AbilityBattleBase` / `AbilityBattleSkill` / `AbilityBattleStatus` / `AbilityBattleProjectile` — `SkillTable.xlsx` generated row wrapper
- `AbilityAffect` — `SkillTable.xlsx -> AFFECT` generated row wrapper
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
| `AbilityBase.cs` | stat dictionary, indexer, `AddStat`, `SetStat`, `ClearStat`, `Clone` 공통 베이스 + aggregate property (`AtkPhysical`, `AtkMagical`, `DefPhysical`, `DefMagical`, `MaxHP`) |
| `AbilityBattleBase.cs` | battle wrapper 공통 abstract 베이스 |
| `AbilityBattleSkill.cs` | `SKILL` 참조 wrapper (`SkillId`, `NameId`, `AffectList`) |
| `AbilityBattleStatus.cs` | `STATUS` 참조 wrapper (`StatusId`, `NameId`, `AffectList`) |
| `AbilityBattleProjectile.cs` | `PROJECTILE` 참조 wrapper (`ProjectileId`, `NameId`, `AffectList`) |
| `AbilityAffect.cs` | `AFFECT` 참조 wrapper (`AffectId`, `NameId`) |
| `AbilityItemBase.cs` | item 공통 abstract 베이스 (`Amount`, `ItemLevel`, `AddAmount`, level stat replace helper) |
| `AbilityItemEquip.cs` | `ITEM_EQUIP` 참조 + `ItemUid` + owner/unit slot 상태 |
| `AbilityEquipSlotPolicy.cs` | `EQUIP_SLOT` 기반 장비-슬롯 허용/양손 규칙 helper |
| `AbilityItemCard.cs` | `ITEM_CARD` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 |
| `AbilityItemMaterial.cs` | `ITEM_MATERIAL` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 |
| `AbilityItemHero.cs` | `ITEM_HERO` 참조 + `ITEM_AMOUNT`/`ITEM_LEVEL` 기반 상태 관리 + outgame equip slot 소유 |
| `AbilityUnitBase.cs` | unit 공통 abstract 베이스 (`UnitId`) |
| `AbilityUnitHero.cs` | `UNIT_HERO` 초기화 + preview용 projected equip snapshot |
| `AbilityUnitMonster.cs` | `UNIT_MONSTER` 초기화 + 기본 stat 세팅 |

---

## 4. Generated Dependencies

### Consumed Generated Types

- `Devian.Domain.Game.UNIT_STAT_TYPE`
- `Devian.Domain.Game.SKILL`
- `Devian.Domain.Game.STATUS`
- `Devian.Domain.Game.PROJECTILE`
- `Devian.Domain.Game.AFFECT`
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

- `AbilityBase`는 `(UNIT_STAT_TYPE, int)` 정규화 저장소를 단일 SSOT로 사용한다.
- `AbilityBase.AtkPhysical`, `AtkMagical`, `DefPhysical`, `DefMagical`는 `(AFFECT_*_ADD + ITEM_*) * (100 + AFFECT_*_PER) / 100 + UNIT_*` 규칙을 따른다. 구현은 정수 결과를 유지한다.
- `AbilityBase.MaxHP`는 `(AFFECT_HP_ADD + ITEM_HP) * (100 + AFFECT_HP_PER) / 100 + UNIT_HP` 규칙을 따른다. 구현은 정수 결과를 유지한다.
- `AbilityBattleSkill`, `AbilityBattleStatus`, `AbilityBattleProjectile`, `AbilityAffect`는 `SkillTable.xlsx` generated row를 참조하는 얇은 wrapper다. 별도 factory나 level table 규약은 두지 않는다.
- `AbilityItemEquip`은 `itemUid`를 인스턴스 pk로 사용하고, 장착 상태는 `OwnerUnitId` + `OwnerSlotType`으로 관리한다.
- 장착 슬롯 enum 정본은 `EQUIP_SLOT_TYPE`이며 `NONE=0`을 포함한다.
- `AbilityItemCard.Amount`, `AbilityItemMaterial.Amount`, `AbilityItemHero.Amount`는 `AbilityItemBase.Amount`를 상속하며, `UNIT_STAT_TYPE.ITEM_AMOUNT`의 얇은 래퍼다.
- `AbilityItemCard.Init()`, `AbilityItemHero.Init()`, `AbilityItemEquip.Init()`는 base `ITEM_*` row와 대응 `ITEM_*_LEVEL` row를 함께 받아서 초기 stat을 세팅한다.
- `AbilityItemBase`는 level row 교체용 helper를 가진다. level up 시 현재 level row stat을 제거하고 다음 level row stat을 적용한다.
- `AbilityItemCard`, `AbilityItemHero`, `AbilityItemEquip`는 internal `_LevelUp()`을 제공한다. 현재 row 기준으로 다음 `ITEM_*_LEVEL` row를 찾아 runtime stat을 재적용한다.
- `AbilityItemHero`는 outgame hero 저장 모델이다. `Equips`와 internal `_SetEquip(equip, slot)` / `_RemoveEquip(slot)`로 loadout metadata만 보유한다.
- `AbilityItemHero` / `AbilityUnitHero`의 장착 슬롯 key는 `Dictionary<EQUIP_SLOT_TYPE, AbilityItemEquip>`다.
- 장착 허용 규칙 정본은 `ITEM_EQUIP.equip_type` + `EQUIP_SLOT.allowed_slots/two_handed` + `AbilityEquipSlotPolicy`다.
- `two_handed=true` 장비를 `HAND_MAIN`에 장착하면 현재 `HAND_SUB` 장비를 자동 해제한다.
- `HAND_MAIN`에 양손 장비가 있는 상태에서 `HAND_SUB` 장착 시도는 실패한다.
- `AbilityItemHero`는 `ITEM_HERO.unit_id`를 참조할 수 있으며, item hero와 unit hero를 명시 필드로 연결한다.
- `AbilityUnitHero`는 `UNIT_HERO` 기반 계산 모델이다. internal `_Equip(equip, slot)` / `_Unequip(slot)`에서 장착된 `AbilityItemEquip` stat을 unit stat에 합산/제거한다. `UNIT_STAT_TYPE.ITEM_LEVEL`, `UNIT_STAT_TYPE.ITEM_AMOUNT` 같은 item 메타 stat은 unit aggregate에 섞지 않는다.
- preview/ingame projection의 equip 계산 규칙 정본은 [15-game-ability-factory](../15-game-ability-factory/SKILL.md)다.
- `AbilityUnitBase`는 `UnitLevel`, `CurHP` 프로퍼티를 제공한다. `CurHP`는 stat dictionary가 아니라 runtime field(`mCurHP`)이며 clone 시 같이 복사된다. `MaxHP`는 `AbilityBase` aggregate property를 사용한다.
- `AbilityUnitHero.Init()` / `AbilityUnitMonster.Init()`는 base `UNIT_*` row와 대응 `UNIT_*_LEVEL` row를 함께 받아서 `UNIT_STAT_TYPE.UNIT_LEVEL`, `UNIT_STAT_TYPE.UNIT_HP`를 초기화하고 `CurHP = MaxHP`로 시작한다.
- clone 동작은 generated table 참조는 유지하고 stat 값만 복사한다.

---

## 6. Hard Rules

- 수동 addon 코드는 `Runtime/Ability/`에만 둔다.
- `Runtime/Generated/`, `Editor/Generated/`는 빌더 관리 영역이다. 수동 수정 금지.
- `UNIT_STAT_TYPE`, `ITEM_*`, `UNIT_*`는 generated 타입이다. 수동 재정의 금지.
- 정본 수정 위치는 항상 `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/`다.
- 장착/해제 규칙을 바꾸면 Inventory 계열 스킬 문서도 함께 갱신한다.

---

## 7. Related

- [devian/21-domain-game/12-game-ability](../../../devian/21-domain-game/12-game-ability/SKILL.md) — Ability feature 모델/TS 관점
- [devian/21-domain-game/13-game-stat-type](../../../devian/21-domain-game/13-game-stat-type/SKILL.md) — `UNIT_STAT_TYPE` 정의
- [15-game-ability-factory](../15-game-ability-factory/SKILL.md) — ability 생성 / projection / preview 규약
- [16-equip-slot-policy](../16-equip-slot-policy/SKILL.md) — equip slot rule / two-handed policy
- [devian-unity/50-mobile-package/22-inventory-system/03-ssot](../../50-mobile-package/22-inventory-system/03-ssot/SKILL.md) — Inventory 상태/Apply 규약
- [devian-unity/50-mobile-package/22-inventory-system/11-inventory-storage](../../50-mobile-package/22-inventory-system/11-inventory-storage/SKILL.md) — Ability 저장/장착 위임
