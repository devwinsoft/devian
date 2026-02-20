# 13-game-stat-type

Status: ACTIVE
AppliesTo: v10

**STAT_TYPE enum 정의.** Game 도메인의 `STAT_TYPE` enum 값(카테고리별)을 관리한다.

---

## 1. Overview

`STAT_TYPE`은 Game 도메인 contract에서 빌드 파이프라인으로 생성되는 enum이다.
모든 Ability 시스템([12-game-ability](../12-game-ability/SKILL.md))의 key로 사용된다.

- 입력: `input/Domains/Game/ENUM_TYPES.json`
- 생성: `Devian.Domain.Game.STAT_TYPE` enum
- 네임스페이스: `Devian.Domain.Game`

이 스킬에 카테고리별 STAT_TYPE 값을 추가/관리한다.

---

## 2. STAT_TYPE Values

### Card (1~)

| name | value | 설명 |
|---|---|---|
| `CARD_AMOUNT` | 1 | 카드 수량(Amount) |
| `CARD_LEVEL` | 2 | 카드 레벨 |

### Equip (10~)

| name | value | 설명 |
|---|---|---|
| `EQUIP_LEVEL` | 11 | 장비 레벨 |

### Unit (20~)

| name | value | 설명 |
|---|---|---|
| `UNIT_AMOUNT` | 20 | 유닛 수량(Amount) |
| `UNIT_LEVEL` | 21 | 유닛 레벨 |
| `UNIT_HP_MAX` | 100 | 유닛 최대 HP |

---

## 3. ENUM_TYPES.json — STAT_TYPE 부분 (SSOT)

```json
{
  "enums": [
    {
      "name": "STAT_TYPE",
      "values": [
        { "name": "NONE", "value": 0 },
        { "name": "CARD_AMOUNT", "value": 1 },
        { "name": "CARD_LEVEL", "value": 2 },
        { "name": "EQUIP_LEVEL", "value": 11 },

        { "name": "UNIT_AMOUNT", "value": 20 },
        { "name": "UNIT_LEVEL", "value": 21 },
        { "name": "UNIT_HP_MAX", "value": 100 }
      ]
    }
  ]
}
```

---

## 4. 사용 예

### AbilityCard 수량 (CARD_AMOUNT)

`AbilityCard[STAT_TYPE.CARD_AMOUNT]`를 사용한다.

```csharp
// 수량 읽기
int amount = abilityCard.Amount;  // = this[STAT_TYPE.CARD_AMOUNT]

// 수량 누적
abilityCard.AddAmount(delta);     // = AddStat(STAT_TYPE.CARD_AMOUNT, delta)
```

- `AbilityBase.mStats`의 `STAT_TYPE.CARD_AMOUNT` 값이 카드 수량 SSOT이다.

### AbilityUnitHero 수량 (UNIT_AMOUNT)

`AbilityUnitHero[STAT_TYPE.UNIT_AMOUNT]`를 사용한다.

```csharp
// 수량 읽기
int amount = hero[STAT_TYPE.UNIT_AMOUNT];

// 수량 누적
hero.AddStat(STAT_TYPE.UNIT_AMOUNT, delta);
```

### AbilityEquip 장착 정보 (Owner)

장착 정보는 STAT_TYPE이 아닌 **AbilityEquip의 별도 필드**로 관리한다.

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

- `STAT_TYPE`은 Generated enum이다. 수동 정의 금지.
- 새 STAT_TYPE 값 추가 시 이 스킬 → `ENUM_TYPES.json` → 빌드 순서로 진행한다.
- value 번호는 카테고리별로 범위를 관리한다 (충돌 방지).

---

## 6. Related

- [12-game-ability](../12-game-ability/SKILL.md) — AbilityBase, AbilityEquip (STAT_TYPE 소비자)
- [11-domain-game](../11-domain-game/SKILL.md) — Game 도메인 허브 (ENUM_TYPES.json contract)
- [11-inventory-storage](../15-game-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage
