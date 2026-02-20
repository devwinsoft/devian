# 13-game-stat-type

Status: ACTIVE
AppliesTo: v10

**StatType enum 정의.** Game 도메인의 `StatType` enum 값(카테고리별)을 관리한다.

---

## 1. Overview

`StatType`은 Game 도메인 contract에서 빌드 파이프라인으로 생성되는 enum이다.
모든 Ability 시스템([12-game-ability](../12-game-ability/SKILL.md))의 key로 사용된다.

- 입력: `input/Domains/Game/EnumTypes.json`
- 생성: `Devian.Domain.Game.StatType` enum
- 네임스페이스: `Devian.Domain.Game`

이 스킬에 카테고리별 StatType 값을 추가/관리한다.

---

## 2. StatType Values

### Card (1~)

| name | value | 설명 |
|---|---|---|
| `CardAmount` | 1 | 카드 수량(Amount) |
| `CardLevel` | 2 | 카드 레벨 |

### Equip (10~)

| name | value | 설명 |
|---|---|---|
| `EquipLevel` | 11 | 장비 레벨 |

### Unit (20~)

| name | value | 설명 |
|---|---|---|
| `UnitAmount` | 20 | 유닛 수량(Amount) |
| `UnitLevel` | 21 | 유닛 레벨 |
| `UnitHpMax` | 100 | 유닛 최대 HP |

---

## 3. EnumTypes.json — StatType 부분 (SSOT)

```json
{
  "enums": [
    {
      "name": "StatType",
      "values": [
        { "name": "None", "value": 0 },
        { "name": "CardAmount", "value": 1 },
        { "name": "CardLevel", "value": 2 },
        { "name": "EquipLevel", "value": 11 },

        { "name": "UnitAmount", "value": 20 },
        { "name": "UnitLevel", "value": 21 },
        { "name": "UnitHpMax", "value": 100 }
      ]
    }
  ]
}
```

---

## 4. 사용 예

### AbilityCard 수량 (CardAmount)

`AbilityCard[StatType.CardAmount]`를 사용한다.

```csharp
// 수량 읽기
int amount = abilityCard.Amount;  // = this[StatType.CardAmount]

// 수량 누적
abilityCard.AddAmount(delta);     // = AddStat(StatType.CardAmount, delta)
```

- `AbilityBase.mStats`의 `StatType.CardAmount` 값이 카드 수량 SSOT이다.

### AbilityUnitHero 수량 (UnitAmount)

`AbilityUnitHero[StatType.UnitAmount]`를 사용한다.

```csharp
// 수량 읽기
int amount = hero[StatType.UnitAmount];

// 수량 누적
hero.AddStat(StatType.UnitAmount, delta);
```

### AbilityEquip 장착 정보 (Owner)

장착 정보는 StatType이 아닌 **AbilityEquip의 별도 필드**로 관리한다.

```csharp
// 장착 여부
bool equipped = abilityEquip.IsEquipped;           // mOwnerSlotNumber > 0

// 소유자 정보
string unitId = abilityEquip.OwnerUnitId;          // 장착된 영웅 UnitId
int slot = abilityEquip.OwnerSlotNumber;           // 장착 슬롯 번호 (0 = 미장착)

// 장착/해제는 AbilityUnitHero.Equip/Unequip을 통해 수행
hero.Equip(equip, slotNumber);
hero.Unequip(slotNumber);
```

---

## 5. Hard Rules

- `StatType`은 Generated enum이다. 수동 정의 금지.
- 새 StatType 값 추가 시 이 스킬 → `EnumTypes.json` → 빌드 순서로 진행한다.
- value 번호는 카테고리별로 범위를 관리한다 (충돌 방지).

---

## 6. Related

- [12-game-ability](../12-game-ability/SKILL.md) — AbilityBase, AbilityEquip (StatType 소비자)
- [11-domain-game](../11-domain-game/SKILL.md) — Game 도메인 허브 (EnumTypes.json contract)
- [11-inventory-storage](../../50-mobile-system/52-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage
