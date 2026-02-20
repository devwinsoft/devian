# 12-game-ability

Status: ACTIVE
AppliesTo: v10

GameContents의 **Ability 시스템**이다.
모든 엔티티(Hero, Item, Skill 등)의 속성 값을 `(StatType, value)` 리스트로 정규화하여 관리한다.

Ability는 **컨텐츠(Game) 레이어에만 존재**한다.
시스템 레이어(MobileSystem/Foundation)는 Ability를 전혀 참조하지 않는다.

---

## 1. StatType (Generated enum)

`StatType`은 Game 도메인 contract에서 빌드 파이프라인으로 생성한다.

- 입력: `input/Domains/Game/EnumTypes.json`
- 생성: `Devian.Domain.Game.StatType` enum
- 네임스페이스: `Devian.Domain.Game`

**StatType 값 정의/관리:** [13-game-stat-type](../13-game-stat-type/SKILL.md)

---

## 2. 클래스 계층

```
AbilityBase              ← Dict<StatType, int>, indexer, GetInt, GetFloat, AddStat, SetStat, ClearStat, GetStats, Clone
  ├─ AbilityEquip        ← 장비 Inventory 연동용 (OwnerUnitId, OwnerSlotNumber, IsEquipped)
  ├─ AbilityCard         ← 카드 Inventory 연동용
  └─ AbilityUnitBase (abstract) ← Unit 공통 (UnitId)
       ├─ AbilityUnitHero    ← UNIT_HERO 테이블 Init, Dict<int, AbilityEquip> mEquips, Equip/Unequip
       └─ AbilityUnitMonster ← UNIT_MONSTER 테이블 Init
```

- **컨텐츠 레이어(GameContents)**에만 위치한다.
- 시스템 레이어는 이 계층을 알지 못한다.
- POCO (MonoBehaviour가 아닌 순수 C# 클래스)이다.

---

## 3. AbilityBase

```csharp
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
    {
        Dictionary<StatType, int> mStats = new();

        public int this[StatType type]
        {
            get => mStats.TryGetValue(type, out var v) ? v : 0;
        }

        public void AddStat(StatType type, int value)
        {
            mStats.TryGetValue(type, out var cur);
            mStats[type] = cur + value;
        }

        public void AddStat(AbilityBase other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public int GetInt(StatType type) => mStats.TryGetValue(type, out var v) ? v : 0;

        public float GetFloat(StatType type) => GetInt(type) * 0.0001f;

        public void SetStat(StatType type, int value) => mStats[type] = value;

        public void ClearStat(StatType type) => mStats.Remove(type);

        public void ClearStats() => mStats.Clear();

        public IReadOnlyDictionary<StatType, int> GetStats() => mStats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }
    }
}
```

- `Dictionary<StatType, int>` — 스탯 정규화 저장소
- indexer `this[StatType]` — 없는 키는 `0` 반환
- `GetInt(type)` — indexer와 동일 (명시적 int 반환)
- `GetFloat(type)` — 1만분율 변환 (stat value 1 → 0.0001f)
- `AddStat(type, value)` — 누적 합산
- `AddStat(AbilityBase)` — 다른 Ability의 스탯 전체를 합산 (버프/장비 합산)
- `SetStat(type, value)` — 특정 stat을 절대값으로 설정 (기존값 무시, 덮어쓰기)
- `ClearStat(type)` — 특정 stat 제거 (dict에서 key 삭제, indexer 조회 시 0 반환)
- `ClearStats()` — 전체 stat 초기화
- `GetStats()` — `IReadOnlyDictionary<StatType, int>` 반환 (직렬화/열거용 read-only view)
- `Clone()` — abstract. leaf 클래스가 override하여 자기 타입 인스턴스를 생성하고 mTable 참조(shallow) + mStats 값(deep)을 복사한다.
- `CopyStatsFrom(source)` — protected. Clone() 구현에서 mStats dict를 deep copy하는 헬퍼.

---

## 4. AbilityEquip / AbilityCard

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityEquip : AbilityBase
    {
        EQUIP mTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        int mOwnerSlotNumber = 0;

        public string ItemUid => mItemUid;
        public string EquipId => mTable?.EquipId ?? string.Empty;
        public string OwnerUnitId => mOwnerUnitId;
        public int OwnerSlotNumber => mOwnerSlotNumber;
        public bool IsEquipped => mOwnerSlotNumber > 0;

        public void Init(EQUIP table, string itemUid)
        {
            mTable = table;
            mItemUid = itemUid;
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
    public sealed class AbilityCard : AbilityBase
    {
        CARD mTable = null;

        public string CardId => mTable?.CardId ?? string.Empty;

        public void Init(CARD table)
        {
            mTable = table;
        }
    }
}
```

- `AbilityEquip` — EQUIP 테이블 entity를 직접 참조하여 초기화한다. `ItemUid`(인스턴스 고유 GUID)와 `EquipId`(템플릿 ID) 프로퍼티 노출. 같은 `equipId`에 여러 인스턴스가 존재할 수 있다.
- `AbilityEquip`: `mTable` 참조 + `Init(table, itemUid)` + `ItemUid` + `OwnerUnitId` + `OwnerSlotNumber` + `IsEquipped` + `SetOwner(unitId, slot)` + `ClearOwner()` + `Clone()`. pk는 `itemUid`(GUID).
- `AbilityCard` — CARD 테이블 entity를 직접 참조하여 초기화한다. `CardId` 프로퍼티 노출. `CARD`는 Generated entity (TB_CARD 컨테이너).
- `AbilityCard`: `mTable` 참조 + `Init(table)` + `Amount` + `AddAmount(delta)` + `Clone()`.

---

## 5. AbilityUnitBase / AbilityUnitHero / AbilityUnitMonster

```csharp
namespace Devian
{
    public abstract class AbilityUnitBase : AbilityBase
    {
        public abstract string UnitId { get; }
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
        readonly Dictionary<int, AbilityEquip> mEquips = new();

        public override string UnitId => mTable?.UnitId ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityEquip> Equips => mEquips;

        public void Init(UNIT_HERO table)
        {
            mTable = table;
            AddStat(StatType.UnitHpMax, table.MaxHp);
        }

        public bool Equip(AbilityEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0) return false;
            if (equip.IsEquipped) equip.ClearOwner();
            if (mEquips.TryGetValue(slotNumber, out var prev))
                prev.ClearOwner();
            mEquips[slotNumber] = equip;
            equip.SetOwner(UnitId, slotNumber);
            return true;
        }

        public bool Unequip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip)) return false;
            equip.ClearOwner();
            mEquips.Remove(slotNumber);
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

        public override string UnitId => mTable?.UnitId ?? string.Empty;

        public void Init(UNIT_MONSTER table)
        {
            mTable = table;
            AddStat(StatType.UnitHpMax, table.MaxHp);
        }
    }
}
```

- `AbilityUnitBase`는 abstract — Unit 공통 계층. `UnitId` 추상 프로퍼티를 정의한다.
- `AbilityUnitHero`는 `UNIT_HERO` 테이블 entity를 참조하여 초기화한다. `Init()`에서 `UnitHpMax` stat을 설정한다. `Dict<int, AbilityEquip> mEquips`로 슬롯별 장비를 직접 소유한다. `Equip(equip, slot)` / `Unequip(slot)` 메서드를 제공한다.
- `AbilityUnitMonster`는 `UNIT_MONSTER` 테이블 entity를 참조하여 초기화한다. `Init()`에서 `UnitHpMax` stat을 설정한다.
- `UNIT_HERO`, `UNIT_MONSTER`는 `Devian.Domain.Game` 네임스페이스의 Generated entity (UnitTable.xlsx).

---

## 6. Implementation Location (SSOT)

### Ability 클래스 (GameContents 샘플)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/GameContents/Runtime/Ability/`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/GameContents/Runtime/Ability/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/GameContents/Runtime/Ability/`

파일 목록:
```
Ability/
├─ AbilityBase.cs
├─ AbilityEquip.cs
├─ AbilityCard.cs
├─ AbilityUnitBase.cs
├─ AbilityUnitHero.cs
└─ AbilityUnitMonster.cs
```

### StatType contract (입력)

- `input/Domains/Game/EnumTypes.json`

### StatType enum (Generated)

- `Devian.Domain.Game` 패키지 내 Generated 코드

---

## 7. asmdef

`Devian.Samples.GameContents.asmdef`에 이미 포함된 참조로 충분하다:
- `Devian.Domain.Game` (StatType enum 참조)

추가 참조 불필요.

---

## 8. namespace

```csharp
namespace Devian
```

(Samples 정책: 단일 namespace `Devian`)

`StatType` enum만 `Devian.Domain.Game` 네임스페이스이므로 `using Devian.Domain.Game;`이 필요하다.

---

## 9. Hard Rules

- Ability 계층은 **컨텐츠 레이어(GameContents)에만 존재**한다.
- 시스템 레이어(MobileSystem/Foundation)는 Ability를 참조하지 않는다.
- `StatType`은 Generated enum이다. 수동 정의 금지.
- stat value 타입은 `int`이다.
- POCO이다 (MonoBehaviour 상속 금지).
- `AbilityBase`의 `mStats`는 `Dictionary<StatType, int>`이다 (정규화 SSOT).
- "Sample" 접두사 금지 (정책).

---

## 10. Devian 예제 범위

- `AbilityBase`, `AbilityEquip`, `AbilityCard`, `AbilityUnitBase`, `AbilityUnitHero`, `AbilityUnitMonster` 6개를 구현한다.
- `AbilityEquip`은 장비 Inventory에서 사용한다.
- `AbilityCard`는 카드 Inventory에서 사용한다.
- `AbilityUnitBase` 계층은 Unit 시스템에서 사용할 예정이다.

---

## 11. Related

- [13-game-stat-type](../13-game-stat-type/SKILL.md) — StatType enum 값 정의/관리
- [00-overview](../00-overview/SKILL.md) — GameContents 개요
- [11-inventory-storage](../../50-mobile-system/52-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage (AbilityEquip, AbilityCard 사용처)
- [10-inventory-manager](../../50-mobile-system/52-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유)
