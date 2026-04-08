# 13-game-stat-type

Status: ACTIVE
AppliesTo: v10

**UNIT_STAT_TYPE enum 정의.** Game 도메인의 `UNIT_STAT_TYPE` enum 값(카테고리별)을 관리한다.

---

## 1. Overview

`UNIT_STAT_TYPE`은 Game 도메인 contract에서 빌드 파이프라인으로 생성되는 enum이다.
모든 Ability 시스템([12-game-ability](../12-game-ability/SKILL.md), [devian-unity/21-game-package/12-game-ability](../../../devian-unity/21-game-package/12-game-ability/SKILL.md))의 key로 사용된다.

- 입력: `input/Domains/Game/ENUM_UNIT.json`
- 생성: `Devian.Domain.Game.UNIT_STAT_TYPE` enum
- 네임스페이스: `Devian.Domain.Game`

이 스킬에 카테고리별 UNIT_STAT_TYPE 값을 추가/관리한다.

---

## 2. UNIT_STAT_TYPE Values

### Item (1~)

| name | value | 설명 |
|---|---|---|
| `ITEM_AMOUNT` | 1 | item-like 엔티티(카드/영웅/재료) 수량(Amount) |
| `ITEM_LEVEL` | 2 | item-like 엔티티(카드/영웅/재료) 레벨 |

### Unit

| name | value | 설명 |
|---|---|---|
| `UNIT_HP_CUR` | 101 | 유닛 현재 HP(runtime current state) |
| `UNIT_HP` | 102 | 유닛 HP |
| `UNIT_LEVEL` | 103 | 유닛 레벨 |

---

## 3. ENUM_UNIT.json — UNIT_STAT_TYPE 부분 (SSOT)

```json
{
  "enums": [
    {
      "name": "UNIT_STAT_TYPE",
      "values": [
        { "name": "NONE", "value": 0 },
        { "name": "ITEM_AMOUNT", "value": 1 },
        { "name": "ITEM_LEVEL", "value": 2 },
        { "name": "UNIT_HP_CUR", "value": 101 },
        { "name": "UNIT_HP", "value": 102 },
        { "name": "UNIT_LEVEL", "value": 103 }
      ]
    }
  ]
}
```

---

## 4. 사용 예

### AbilityItemCard / AbilityItemMaterial 수량 (ITEM_AMOUNT)

`AbilityItemCard[UNIT_STAT_TYPE.ITEM_AMOUNT]`, `AbilityItemMaterial[UNIT_STAT_TYPE.ITEM_AMOUNT]`를 사용한다.

```csharp
// 수량 읽기
int amount = abilityCard.Amount;      // = this[UNIT_STAT_TYPE.ITEM_AMOUNT]
int matAmount = abilityMaterial.Amount;

// 수량 누적
abilityCard.AddAmount(delta);         // = AddStat(UNIT_STAT_TYPE.ITEM_AMOUNT, delta)
abilityMaterial.AddAmount(delta);
```

- `AbilityBase.mStats`의 `UNIT_STAT_TYPE.ITEM_AMOUNT` 값이 카드/재료 수량 SSOT이다.

### AbilityItemHero 수량 (ITEM_AMOUNT)

`AbilityItemHero[UNIT_STAT_TYPE.ITEM_AMOUNT]`를 사용한다.

```csharp
// 수량 읽기
int amount = hero.Amount;

// 수량 누적
hero.AddAmount(delta);
```

### AbilityItemEquip 장착 정보 (Owner)

장착 정보는 STAT_TYPE이 아닌 **AbilityItemEquip의 별도 필드**로 관리한다.

```csharp
// 장착 여부
bool equipped = abilityEquip.IsEquipped;           // mOwnerSlotNumber > 0

// 소유자 정보
string ownerItemId = abilityEquip.OwnerUnitId;     // 장착된 영웅 inventory id
int slot = abilityEquip.OwnerSlotNumber;           // 장착 슬롯 번호 (0 = 미장착)

// 저장 모델 장착/해제는 AbilityItemHero.SetEquip/RemoveEquip을 통해 수행
hero.SetEquip(equip, slotNumber);
hero.RemoveEquip(slotNumber);
```

---

## 5. Hard Rules

- `UNIT_STAT_TYPE`은 Generated enum이다. 수동 정의 금지.
- 새 UNIT_STAT_TYPE 값 추가 시 이 스킬 → `ENUM_UNIT.json` → 빌드 순서로 진행한다.
- value 번호는 카테고리별로 범위를 관리한다 (충돌 방지).

---

## 6. Related

- [12-game-ability](../12-game-ability/SKILL.md) — AbilityBase, AbilityItemEquip (UNIT_STAT_TYPE 소비자)
- [devian-unity/21-game-package/12-game-ability](../../../devian-unity/21-game-package/12-game-ability/SKILL.md) — Unity GamePackage Ability addon
- [11-game-tables](../11-game-tables/SKILL.md) — Game 도메인 테이블 정의
