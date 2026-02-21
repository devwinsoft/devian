# 03-ssot — 15-game-inventory-system (SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

- RewardData 적용 규칙(Inventory 관점)
- 인벤토리 상태 표현(개념)
- Apply 원자성/에러 처리 규칙


---


## A) RewardData Contract Source (정본)

- `RewardData` 스키마의 단일 정본은 아래 문서다:
  - [49-reward-system/03-ssot](../../../50-mobile-system/49-reward-system/03-ssot/SKILL.md)
- 본 문서(15-game-inventory-system)는 `RewardData` 필드 스키마를 재정의하지 않는다.
- Inventory는 위 정본 규약을 입력 계약으로 사용한다.


---


## B) Inventory State (개념)

Inventory 상태는 "통화", "아이템", "영웅"으로 분리된다.

### B-1) Wallet

- key: `currencyType` (=`RewardData.id` when `type=REWARD_TYPE.CURRENCY`)
- value: `amount (long)`

### B-2) Equips

- key: `itemUid` (string, GUID, 인스턴스별 고유 pk)
- value: `AbilityEquip`

`AbilityEquip` 필드 (구현: [12-game-ability](../../12-game-ability/SKILL.md)):
- `ItemUid: string` (== key, 인스턴스 고유 ID, GUID)
- `EquipId: string` (템플릿 ID, `mTable.EquipId`)
- `OwnerUnitId: string` (장착된 영웅 UnitId, 미장착 시 empty)
- `OwnerSlotNumber: int` (장착 슬롯 번호, 0 = 미장착)
- `IsEquipped: bool` (= `OwnerSlotNumber > 0`)
- 능력치: `AbilityEquip : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 레벨 = `STAT_TYPE.EQUIP_LEVEL`

NOTE:
- 같은 `equipId`에 여러 인스턴스(각각 고유 `itemUid`)가 존재할 수 있다.
- `RewardData.Id`는 `equipId`(템플릿 ID)이다. `itemUid`는 InventoryManager가 Apply 시 생성한다.
- `ItemData` 클래스는 `AbilityEquip`에 통합되어 삭제되었다.

### B-3) Cards

- key: `cardId` (=`RewardData.id` when `type=REWARD_TYPE.CARD`, pk)
- value: `AbilityCard`

`AbilityCard` 필드 (구현: [12-game-ability](../../12-game-ability/SKILL.md)):
- `CardId: string` (== key, `mTable.CardId`)
- `Amount: int` (= `this[STAT_TYPE.CARD_AMOUNT]`)
- 능력치: `AbilityCard : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 수량 = `STAT_TYPE.CARD_AMOUNT`
  - 레벨 = `STAT_TYPE.CARD_LEVEL`
  - Reward/Purchase grants에서는 `STAT_TYPE.CARD_AMOUNT`만 변경된다

### B-4) Heroes

- key: `heroId` (=`RewardData.id` when `type=REWARD_TYPE.HERO`, pk)
- value: `AbilityUnitHero`

`AbilityUnitHero` 필드 (구현: [12-game-ability](../../12-game-ability/SKILL.md)):
- `UnitId: string` (== key, `mTable.UnitId`)
- 수량 = `STAT_TYPE.UNIT_AMOUNT` (Reward grants에서 변경되는 유일한 stat)
- 능력치: `AbilityUnitHero : AbilityUnitBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)


---


## C) Apply Rules (정본)

### C-1) 공통

- `AddRewards`의 반환 타입은 `CommonResult`다.
- 입력 검증은 `RewardData` 정본([49-reward-system/03-ssot](../../../50-mobile-system/49-reward-system/03-ssot/SKILL.md))을 따른다.
- `rewards.Length == 0`은 valid no-op이다 (`CommonResult.Ok()`).
- invalid가 하나라도 있으면 `CommonResult.Failure`를 반환하고 전체 미적용(원자성)한다.
- 차감/소비/회수(환불/철회 포함)는 RewardData로 처리하지 않는다(별도 시스템/경로).

### C-2) `type == REWARD_TYPE.CURRENCY`

- `_storage.AddCurrency(currencyType, amount)`
- 없는 키는 생성된다.

### C-3) `type == REWARD_TYPE.EQUIP`

- 매 Apply마다 새 `itemUid`(GUID)를 생성하여 새 AbilityEquip 인스턴스를 추가한다.
- `_storage.AddEquip(itemUid, ability)`로 생성된다.
  - 새 AbilityEquip의 모든 stat은 0(기본값)으로 시작한다.
- `amount`는 무시한다 (항상 1개 인스턴스 생성).

### C-5) `type == REWARD_TYPE.CARD`

- `_storage.Cards[cardId].AddAmount(amount)` (= `AddStat(STAT_TYPE.CARD_AMOUNT, amount)`)
- 없는 키는 `_storage.AddCard(cardId, ability)`로 생성된다.
  - 새 AbilityCard의 모든 stat은 0(기본값)으로 시작한다.
- Apply는 `STAT_TYPE.CARD_AMOUNT`만 변경한다 (다른 stat은 보존).

### C-4) `type == REWARD_TYPE.HERO`

- `_storage.Heroes[heroId].AddStat(STAT_TYPE.UNIT_AMOUNT, amount)`
- 없는 키는 `_storage.AddHero(heroId, ability)`로 생성된다.
  - 새 AbilityUnitHero는 `TB_UNIT_HERO.Get(heroId)`로 Init한다.
- Apply는 `STAT_TYPE.UNIT_AMOUNT`만 변경한다 (다른 stat은 보존).


---


## C-6) 장비 장착/해제

InventoryStorage가 hero/equip 조회 + AbilityUnitHero에 위임하는 편의 메서드를 제공한다.

- `Equip(string heroId, int equipSlot, string equipUid)`:
  1. `mHeroes[heroId]` 조회 → 없으면 false
  2. `mEquipments[equipUid]` 조회 → 없으면 false
  3. `hero.Equip(equip, equipSlot)` 위임
- `Unequip(string heroId, int equipSlot)`:
  1. `mHeroes[heroId]` 조회 → 없으면 false
  2. `hero.Unequip(equipSlot)` 위임


---


## D) JSON Persistence Schema (정본)

> **변경**: `InventoryStorage.ToJson()`/`FromJson()` 메서드는 **삭제**되었다.
> 직렬화 책임은 [16-game-storage-manager](../../16-game-storage-manager/SKILL.md)의 **GameStorageManager**가 담당한다.
> 아래 스키마는 GameStorageManager JSON의 `"inventory"` 섹션에 해당한다.

Inventory 직렬화 스키마 정본 (GameStorageManager.inventory 섹션).

```json
{
  "wallet": { "<currencyId>": <long> },
  "equipments": {
    "<itemUid>": {
      "equipId": "<string>",
      "itemUid": "<string>",
      "ownerUnitId": "<string>",
      "ownerSlotNumber": <int>,
      "stats": { "<STAT_TYPE.ToString()>": <int> }
    }
  },
  "cards": {
    "<cardId>": {
      "cardId": "<string>",
      "stats": { "<STAT_TYPE.ToString()>": <int> }
    }
  },
  "heroes": {
    "<heroId>": {
      "unitId": "<string>",
      "stats": { "<STAT_TYPE.ToString()>": <int> },
      "equips": { "<slotNumber>": "<equipUid>" }
    }
  }
}
```

- STAT_TYPE key: enum name 문자열 (예: `"EQUIP_LEVEL"`, `"CARD_AMOUNT"`)
- Hero equips: slotNumber(string key) → equipUid(string value). 중복 데이터 없음.
- 역직렬화 시 테이블 참조: `TB_EQUIP.Get`/`TB_CARD.Get`/`TB_UNIT_HERO.Get`으로 `mTable` 복원.
- 역직렬화 순서: wallet → equipments → cards → heroes (heroes 마지막: equip 슬롯 참조 필요).


---


## E) Error Code Source (정본)

- `AddRewards` 실패 에러 코드는 `CommonErrorType`을 사용한다.
- inventory 전용 에러 코드는 `ERROR_COMMON`을 SSOT로 추가/관리한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `ERROR_COMMON`
