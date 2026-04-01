# 12-game-ability

Status: ACTIVE
AppliesTo: v10

Game 도메인의 **Ability feature layer**이다.
모든 엔티티(Hero, Item, Skill 등)의 속성 값을 `(STAT_TYPE, value)` 리스트로 정규화하여 관리한다.
TypeScript 모듈 관점 설명을 포함하며, Unity `GamePackage` C# addon 구현 정본은 [devian-unity/21-game-package/12-game-ability](../../../devian-unity/21-game-package/12-game-ability/SKILL.md)다.

---

## 1. STAT_TYPE (Generated enum)

`STAT_TYPE`은 Game 도메인 contract에서 빌드 파이프라인으로 생성한다.

- 입력: `input/Domains/Game/ENUM_GAME.json`
- 생성: `Devian.Domain.Game.STAT_TYPE` enum
- 네임스페이스: `Devian.Domain.Game`

**STAT_TYPE 값 정의/관리:** [13-game-stat-type](../13-game-stat-type/SKILL.md)

---

## 2. 클래스 계층

```
AbilityBase              ← Dict<STAT_TYPE, int>, indexer, GetInt, GetFloat, AddStat, SetStat, ClearStat, GetStats, Clone, aggregate property(AtkPhysical/AtkMagical/DefPhysical/DefMagical/MaxHP)
  ├─ AbilityBattleBase (abstract) ← battle wrapper 공통 베이스
  │   ├─ AbilityBattleSkill      ← SKILL row wrapper
  │   ├─ AbilityBattleStatus     ← STATUS row wrapper
  │   └─ AbilityBattleProjectile ← PROJECTILE row wrapper
  ├─ AbilityAffect               ← AFFECT row wrapper
  ├─ AbilityItemBase (abstract) ← Amount, ItemLevel, AddAmount
  │   ├─ AbilityItemEquip   ← 장비 Inventory 연동용 (OwnerUnitId, OwnerSlotNumber, IsEquipped)
  │   ├─ AbilityItemCard    ← 카드 Inventory 연동용
  │   ├─ AbilityItemMaterial ← 재료 Inventory 연동용
  │   └─ AbilityItemHero    ← 영웅 Inventory 연동용, Dict<int, AbilityItemEquip> mEquips, Equip/Unequip
  └─ AbilityUnitBase (abstract) ← Unit 공통 (UnitId)
       ├─ AbilityUnitHero    ← UNIT_HERO 테이블 Init, ingame/unit stat + projected equip snapshot
       └─ AbilityUnitMonster ← UNIT_MONSTER 테이블 Init
```

- Unity C# 구현은 POCO (MonoBehaviour가 아닌 순수 C# 클래스)이고, TS 구현도 동일한 stat 모델을 유지한다.

---

## 3. AbilityBase

```csharp
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
    {
        Dictionary<STAT_TYPE, int> mStats = new();
        public int AtkPhysical => (int)((this[STAT_TYPE.AFFECT_ATK_PHY_ADD] + this[STAT_TYPE.ITEM_ATK_PHY])
            * (100 + this[STAT_TYPE.AFFECT_ATK_PHY_PER]) * 0.01f)
            + this[STAT_TYPE.UNIT_ATK_PHY];
        public int AtkMagical => (int)((this[STAT_TYPE.AFFECT_ATK_MAG_ADD] + this[STAT_TYPE.ITEM_ATK_MAG])
            * (100 + this[STAT_TYPE.AFFECT_ATK_MAG_PER]) * 0.01f)
            + this[STAT_TYPE.UNIT_ATK_MAG];
        public int DefPhysical => (int)((this[STAT_TYPE.AFFECT_DEF_PHY_ADD] + this[STAT_TYPE.ITEM_DEF_PHY])
            * (100 + this[STAT_TYPE.AFFECT_DEF_PHY_PER]) * 0.01f)
            + this[STAT_TYPE.UNIT_DEF_PHY];
        public int DefMagical => (int)((this[STAT_TYPE.AFFECT_DEF_MAG_ADD] + this[STAT_TYPE.ITEM_DEF_MAG])
            * (100 + this[STAT_TYPE.AFFECT_DEF_MAG_PER]) * 0.01f)
            + this[STAT_TYPE.UNIT_DEF_MAG];
        public int MaxHP => (int)((this[STAT_TYPE.AFFECT_HP_ADD] + this[STAT_TYPE.ITEM_HP])
            * (100 + this[STAT_TYPE.AFFECT_HP_PER]) * 0.01f)
            + this[STAT_TYPE.UNIT_HP];

        public int this[STAT_TYPE type]
        {
            get => mStats.TryGetValue(type, out var v) ? v : 0;
        }

        public void AddStat(STAT_TYPE type, int value)
        {
            mStats.TryGetValue(type, out var cur);
            mStats[type] = cur + value;
        }

        public void AddStat(AbilityBase other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public int GetInt(STAT_TYPE type) => mStats.TryGetValue(type, out var v) ? v : 0;

        public float GetFloat(STAT_TYPE type) => GetInt(type) * 0.0001f;

        public void SetStat(STAT_TYPE type, int value) => mStats[type] = value;

        public void ClearStat(STAT_TYPE type) => mStats.Remove(type);

        public void ClearStats() => mStats.Clear();

        public IReadOnlyDictionary<STAT_TYPE, int> GetStats() => mStats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }
    }
}
```

- `Dictionary<STAT_TYPE, int>` — 스탯 정규화 저장소
- `AtkPhysical`, `AtkMagical`, `DefPhysical`, `DefMagical` — `(AFFECT_*_ADD + ITEM_*) * (100 + AFFECT_*_PER) / 100 + UNIT_*` aggregate property
- `MaxHP` — `(AFFECT_HP_ADD + ITEM_HP) * (100 + AFFECT_HP_PER) / 100 + UNIT_HP` aggregate property
- indexer `this[STAT_TYPE]` — 없는 키는 `0` 반환
- `GetInt(type)` — indexer와 동일 (명시적 int 반환)
- `GetFloat(type)` — 1만분율 변환 (stat value 1 → 0.0001f)
- `AddStat(type, value)` — 누적 합산
- `AddStat(AbilityBase)` — 다른 Ability의 스탯 전체를 합산 (버프/장비 합산)
- `SetStat(type, value)` — 특정 stat을 절대값으로 설정 (기존값 무시, 덮어쓰기)
- `ClearStat(type)` — 특정 stat 제거 (dict에서 key 삭제, indexer 조회 시 0 반환)
- `ClearStats()` — 전체 stat 초기화
- `GetStats()` — `IReadOnlyDictionary<STAT_TYPE, int>` 반환 (직렬화/열거용 read-only view)
- `Clone()` — abstract. leaf 클래스가 override하여 자기 타입 인스턴스를 생성하고 mTable 참조(shallow) + mStats 값(deep)을 복사한다.
- `CopyStatsFrom(source)` — protected. Clone() 구현에서 mStats dict를 deep copy하는 헬퍼.

---

## 4. AbilityBattleBase / AbilityBattleSkill / AbilityBattleStatus / AbilityBattleProjectile / AbilityAffect

```csharp
using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBattleBase : AbilityBase
    {
    }

    public sealed class AbilityBattleSkill : AbilityBattleBase
    {
        SKILL mTable = null;

        public string SkillId => mTable?.skill_id ?? string.Empty;
        public string NameId => mTable?.name_id ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable?.affect_list ?? Array.Empty<string>();

        public void Init(SKILL table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityBattleSkill();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
```

- `AbilityBattleBase` — `AbilityBase` 위에 얹히는 battle wrapper 공통 abstract 베이스
- `AbilityBattleSkill` — `SKILL` generated row wrapper
- `AbilityBattleStatus` — `STATUS` generated row wrapper
- `AbilityBattleProjectile` — `PROJECTILE` generated row wrapper
- `AbilityAffect` — `AFFECT` generated row wrapper
- battle/affect 계층은 현재 level table/factory 없이 generated row 참조와 stat clone만 담당한다.

## 5. AbilityItemBase / AbilityItemEquip / AbilityItemCard / AbilityItemMaterial / AbilityItemHero

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityItemBase : AbilityBase
    {
        public abstract string ItemId { get; }
        public int Amount => this[STAT_TYPE.ITEM_AMOUNT];
        public int ItemLevel => this[STAT_TYPE.ITEM_LEVEL];
        public void AddAmount(int delta) => AddStat(STAT_TYPE.ITEM_AMOUNT, delta);
    }
}
```

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemEquip : AbilityItemBase
    {
        ITEM_EQUIP mTable = null;
        ITEM_EQUIP_LEVEL mLevelTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        int mOwnerSlotNumber = 0;

        public string ItemUid => mItemUid;
        public override string ItemId => mTable?.item_id ?? string.Empty;
        public string OwnerUnitId => mOwnerUnitId;
        public int OwnerSlotNumber => mOwnerSlotNumber;
        public bool IsEquipped => mOwnerSlotNumber > 0;

        public void Init(ITEM_EQUIP table, ITEM_EQUIP_LEVEL levelTable, string itemUid)
        {
            mTable = table;
            mLevelTable = levelTable;
            mItemUid = itemUid;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemEquip();
            c.mTable = mTable;
            c.mItemUid = mItemUid;
            c.mOwnerUnitId = mOwnerUnitId;
            c.mOwnerSlotNumber = mOwnerSlotNumber;
            c.CopyStatsFrom(this);
            return c;
        }

        public void SetOwner(string unitId, int slotNumber)
        {
            mOwnerUnitId = unitId;
            mOwnerSlotNumber = slotNumber;
        }

        public void ClearOwner()
        {
            mOwnerUnitId = string.Empty;
            mOwnerSlotNumber = 0;
        }
    }
}
```

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemCard : AbilityItemBase
    {
        ITEM_CARD mTable = null;
        ITEM_CARD_LEVEL mLevelTable = null;

        public override string ItemId => mTable?.item_id ?? string.Empty;

        public void Init(ITEM_CARD table, ITEM_CARD_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemCard();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
```

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemMaterial : AbilityItemBase
    {
        ITEM_MATERIAL mTable = null;

        public override string ItemId => mTable?.item_id ?? string.Empty;

        public void Init(ITEM_MATERIAL table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemMaterial();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
```

- `AbilityItemBase` — item 공통 abstract 베이스. `abstract ItemId`, `Amount`(`STAT_TYPE.ITEM_AMOUNT`), `ItemLevel`(`STAT_TYPE.ITEM_LEVEL`), `AddAmount(delta)` 공통 프로퍼티/메서드를 제공한다.
- `AbilityItemEquip` — ITEM_EQUIP 테이블 entity와 ITEM_EQUIP_LEVEL row를 함께 받아 초기화한다. `ItemUid`(인스턴스 고유 GUID)와 `ItemId`(템플릿 ID) 프로퍼티 노출. 같은 `ItemId`에 여러 인스턴스가 존재할 수 있다.
- `AbilityItemEquip`: `mTable` 참조 + `Init(table, levelTable, itemUid)` + `ItemUid` + `OwnerUnitId` + `OwnerSlotNumber` + `IsEquipped` + `SetOwner(unit_id, slot)` + `ClearOwner()` + `Clone()`. pk는 `itemUid`(GUID).
- `AbilityItemCard` — ITEM_CARD 테이블 entity를 직접 참조하여 초기화한다. `ITEM_CARD`는 Generated entity (TB_ITEM_CARD 컨테이너).
- `AbilityItemCard`: `mTable` 참조 + `Init(table, levelTable)` + `Clone()`. `ItemId`/`Amount`/`ItemLevel`/`AddAmount`는 `AbilityItemBase` 상속.
- `AbilityItemMaterial` — `ITEM_MATERIAL` 테이블 entity를 직접 참조하여 초기화한다.
- `AbilityItemMaterial`: `mTable` 참조 + `Init(table)` + `Clone()`. `ItemId`/`Amount`/`ItemLevel`/`AddAmount`는 `AbilityItemBase` 상속.
- `AbilityItemHero` — `ITEM_HERO` 테이블 entity와 ITEM_HERO_LEVEL row를 함께 받아 초기화한다. outgame inventory의 hero 수량/레벨/equip slot 저장 SSOT다.
- `AbilityItemHero`: `mTable` 참조 + `Init(table, levelTable)` + `UnitId` + `Equips` + `SetEquip(equip, slot)` + `RemoveEquip(slot)` + `Clone()`. `ItemId`/`Amount`/`ItemLevel`/`AddAmount`는 `AbilityItemBase` 상속.
- `AbilityItemHero`는 equip stat을 직접 계산하지 않는다. loadout metadata와 equip owner metadata만 유지한다.

---

## 6. AbilityUnitBase / AbilityUnitHero / AbilityUnitMonster

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityUnitBase : AbilityBase
    {
        public abstract string UnitId { get; }
        int mCurHP = 0;

        public int UnitLevel => this[STAT_TYPE.UNIT_LEVEL];
        public int CurHP => mCurHP;
    }
}
```

```csharp
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        UNIT_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public override string UnitId => mTable?.unit_id ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

        public void Init(UNIT_HERO table, UNIT_HERO_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;
            InitUnitState(levelTable.unit_level, levelTable.max_hp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitHero();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            c.CopyUnitStateFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        public bool Equip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (equip.IsEquipped)
                equip.ClearOwner();

            if (mEquips.TryGetValue(slotNumber, out var prev))
                prev.ClearOwner();

            mEquips[slotNumber] = equip;
            equip.SetOwner(UnitId, slotNumber);
            return true;
        }
    }
}
```

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitMonster : AbilityUnitBase
    {
        UNIT_MONSTER mTable = null;
        UNIT_MONSTER_LEVEL mLevelTable = null;

        public override string UnitId => mTable?.unit_id ?? string.Empty;

        public void Init(UNIT_MONSTER table, UNIT_MONSTER_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;
            InitUnitState(levelTable.unit_level, levelTable.max_hp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitMonster();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            c.CopyUnitStateFrom(this);
            return c;
        }
    }
}
```

- `AbilityUnitBase`는 abstract — Unit 공통 계층. `UnitId`, `UnitLevel`, `CurHP` 프로퍼티를 정의한다. `CurHP`는 runtime field(`mCurHP`)이고, 초기화 시 `MaxHP`와 동일하게 시작한다. `MaxHP`는 `AbilityBase` aggregate property를 사용한다.
- `AbilityUnitHero`는 `UNIT_HERO` base row와 `UNIT_HERO_LEVEL` row를 함께 받아 초기화한다. `Init()`에서 `UNIT_LEVEL`, `UNIT_HP` stat을 설정하고 `CurHP = MaxHP`로 맞춘다. `Dict<int, AbilityItemEquip> mEquips`는 preview/ingame projection snapshot이며, outgame ownership 정본은 `AbilityItemHero`다.
- `AbilityUnitMonster`는 `UNIT_MONSTER` base row와 `UNIT_MONSTER_LEVEL` row를 함께 받아 초기화한다. `Init()`에서 `UNIT_LEVEL`, `UNIT_HP` stat을 설정하고 `CurHP = MaxHP`로 맞춘다.
- `UNIT_HERO`, `UNIT_MONSTER`는 `Devian.Domain.Game` 네임스페이스의 Generated entity (UnitTable.xlsx).

---

## 7. Implementation Location

### Unity GamePackage C# addon

- [devian-unity/21-game-package/12-game-ability](../../../devian-unity/21-game-package/12-game-ability/SKILL.md) — `com.devian.foundation/Samples~/GamePackage/Runtime/Ability/` 정본

### TypeScript (`@devian/module-game`)

- `framework-ts/module/devian-domain-game/features/ability/`

```
ability/
├─ AbilityBase.ts
├─ AbilityItemBase.ts
├─ AbilityItemEquip.ts
├─ AbilityItemCard.ts
├─ AbilityItemMaterial.ts
├─ AbilityItemHero.ts
├─ AbilityUnitBase.ts
├─ AbilityUnitHero.ts
├─ AbilityUnitMonster.ts
└─ index.ts
```

---

## 8. Hard Rules

- `STAT_TYPE`은 Generated enum이다. 수동 정의 금지.
- stat value 타입은 `int` (C#) / `number` (TS)이다.
- POCO이다 (MonoBehaviour 상속 금지).
- `AbilityBase`의 `mStats`는 `Dictionary<STAT_TYPE, int>` (C#) / `Map<STAT_TYPE, number>` (TS)이다 (정규화 SSOT).
- Unity GamePackage 경로/asmdef/샘플 배치 규칙은 `devian-unity/21-game-package/12-game-ability`가 정본이다.

---

## 9. Related

- [devian-unity/21-game-package/12-game-ability](../../../devian-unity/21-game-package/12-game-ability/SKILL.md) — Unity GamePackage C# addon 구현
- [13-game-stat-type](../13-game-stat-type/SKILL.md) — STAT_TYPE enum 값 정의/관리
- [00-overview](../00-overview/SKILL.md) — Game 도메인 개요
