# 13-game-stat-type

Status: ACTIVE
AppliesTo: v10

**StatType enum 정의.** Game 도메인의 `StatType` enum 값(카테고리별)을 관리한다.

---

## 1. Overview

`StatType`은 Game 도메인 contract에서 빌드 파이프라인으로 생성되는 enum이다.
모든 Ability 시스템([12-game-ability](../12-game-ability/SKILL.md))의 key로 사용된다.

- 입력: `input/Domains/Game/StatType.json`
- 생성: `Devian.Domain.Game.StatType` enum
- 네임스페이스: `Devian.Domain.Game`

이 스킬에 카테고리별 StatType 값을 추가/관리한다.

---

## 2. StatType Values

### Item

| name | value | 설명 |
|---|---|---|
| `ItemAmount` | 1 | 아이템 수량(Amount) |
| `ItemLevel` | 2 | 아이템 레벨 |
| `ItemSlotNumber` | 3 | 장착 슬롯 인덱스 (0 = 미장착) |

> 추후 Hero, Skill 등 다른 카테고리의 StatType도 이 스킬에 추가한다.

---

## 3. StatType.json (SSOT)

```json
{
  "enums": [
    {
      "name": "StatType",
      "values": [
        { "name": "None", "value": 0 },
        { "name": "ItemAmount", "value": 1 },
        { "name": "ItemLevel", "value": 2 },
        { "name": "ItemSlotNumber", "value": 3 }
      ]
    }
  ]
}
```

---

## 4. 사용 예

### ItemData 수량 (ItemAmount)

`mItemAbility[StatType.ItemAmount]`를 사용한다.

```csharp
// 수량 읽기
int amount = itemData.Ability[StatType.ItemAmount];

// 수량 누적
itemData.Ability.AddStat(StatType.ItemAmount, delta);
```

- `BaseAbility.mStats`의 `StatType.ItemAmount` 값이 아이템 수량 SSOT이다.
- 별도 수량 필드가 불필요해진다 (`ItemData.Amount`로 접근).

### ItemData 장착 슬롯 (ItemSlotNumber)

`mItemAbility[StatType.ItemSlotNumber]`을 사용한다.

```csharp
// 장착 슬롯 읽기
int slot = itemData.Ability[StatType.ItemSlotNumber];  // 0 = 미장착

// 장착 설정
itemData.Ability.SetStat(StatType.ItemSlotNumber, slotNumber);
```

- `BaseAbility.mStats`의 `StatType.ItemSlotNumber` 값이 장착 슬롯 SSOT이다.
- 별도 슬롯 필드가 불필요해진다.

---

## 5. Hard Rules

- `StatType`은 Generated enum이다. 수동 정의 금지.
- 새 StatType 값 추가 시 이 스킬 → `StatType.json` → 빌드 순서로 진행한다.
- value 번호는 카테고리별로 범위를 관리한다 (충돌 방지).

---

## 6. Related

- [12-game-ability](../12-game-ability/SKILL.md) — BaseAbility, ItemAbility (StatType 소비자)
- [11-domain-game](../11-domain-game/SKILL.md) — Game 도메인 허브 (StatType.json contract)
- [11-inventory-storage](../../50-mobile-system/52-inventory-system/11-inventory-storage/SKILL.md) — ItemData (StatType.ItemSlotNumber 사용처)
