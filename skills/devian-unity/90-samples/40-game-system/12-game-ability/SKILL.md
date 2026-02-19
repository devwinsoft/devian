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

- 입력: `input/Domains/Game/StatType.json`
- 생성: `Devian.Domain.Game.StatType` enum
- 네임스페이스: `Devian.Domain.Game`

**StatType 값 정의/관리:** [13-game-stat-type](../13-game-stat-type/SKILL.md)

---

## 2. 클래스 계층

```
BaseAbility              ← Dict<StatType, int>, indexer, AddStat
  └─ ItemAbility         ← Inventory 연동용 (최소 구현)
```

- **컨텐츠 레이어(GameContents)**에만 위치한다.
- 시스템 레이어는 이 계층을 알지 못한다.
- POCO (MonoBehaviour가 아닌 순수 C# 클래스)이다.

---

## 3. BaseAbility

```csharp
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class BaseAbility
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

        public void AddStat(BaseAbility other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public void ClearStats() => mStats.Clear();
    }
}
```

- `Dictionary<StatType, int>` — 스탯 정규화 저장소
- indexer `this[StatType]` — 없는 키는 `0` 반환
- `AddStat(type, value)` — 누적 합산
- `AddStat(BaseAbility)` — 다른 Ability의 스탯 전체를 합산 (버프/장비 합산)

---

## 4. ItemAbility

```csharp
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ItemAbility : BaseAbility
    {
        ITEM mTable = null;

        public string ItemId => mTable?.ItemId ?? string.Empty;

        public void Init(ITEM table)
        {
            mTable = table;
        }
    }
}
```

- ITEM 테이블 entity를 직접 참조하여 초기화한다.
- `ItemId` 프로퍼티 — `mTable.ItemId`를 노출 (ItemData에서 참조).
- `ITEM.ItemId`가 PK이므로 별도 uid 파라미터 불필요.
- `ITEM`은 `Devian.Domain.Game` 네임스페이스의 Generated entity (TB_ITEM 컨테이너).

---

## 5. Implementation Location (SSOT)

### Ability 클래스 (GameContents 샘플)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/GameContents/Runtime/Ability/`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/GameContents/Runtime/Ability/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/GameContents/Runtime/Ability/`

파일 목록:
```
Ability/
├─ BaseAbility.cs
└─ ItemAbility.cs
```

### StatType contract (입력)

- `input/Domains/Game/StatType.json`

### StatType enum (Generated)

- `Devian.Domain.Game` 패키지 내 Generated 코드

---

## 6. asmdef

`Devian.Samples.GameContents.asmdef`에 이미 포함된 참조로 충분하다:
- `Devian.Domain.Game` (StatType enum 참조)

추가 참조 불필요.

---

## 7. namespace

```csharp
namespace Devian
```

(Samples 정책: 단일 namespace `Devian`)

`StatType` enum만 `Devian.Domain.Game` 네임스페이스이므로 `using Devian.Domain.Game;`이 필요하다.

---

## 8. Hard Rules

- Ability 계층은 **컨텐츠 레이어(GameContents)에만 존재**한다.
- 시스템 레이어(MobileSystem/Foundation)는 Ability를 참조하지 않는다.
- `StatType`은 Generated enum이다. 수동 정의 금지.
- stat value 타입은 `int`이다.
- POCO이다 (MonoBehaviour 상속 금지).
- `BaseAbility`의 `mStats`는 `Dictionary<StatType, int>`이다 (정규화 SSOT).
- "Sample" 접두사 금지 (정책).

---

## 9. Devian 예제 범위

- `BaseAbility`와 `ItemAbility` 2개만 구현한다.
- `ItemAbility`는 Inventory에서 사용할 예정이다 (현재 작업 범위 아님).
- BaseAbilityUnit, BaseAbilityBattle 등의 중간 클래스는 필요 시 추가한다.

---

## 10. Related

- [13-game-stat-type](../13-game-stat-type/SKILL.md) — StatType enum 값 정의/관리
- [00-overview](../00-overview/SKILL.md) — GameContents 개요
- [11-inventory-storage](../../50-mobile-system/52-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage / ItemData (ItemAbility 사용처)
- [10-inventory-manager](../../50-mobile-system/52-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (InventoryStorage 소유)
