# 03-ssot — 22-inventory-system (SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

- RewardData 적용 규칙(Inventory 관점)
- 인벤토리 상태 표현(개념)
- Apply 원자성/에러 처리 규칙


---


## A) RewardData Contract Source (정본)

- `RewardData` 스키마의 단일 정본은 아래 문서다:
  - [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
- 본 문서(22-inventory-system)는 `RewardData` 필드 스키마를 재정의하지 않는다.
- Inventory는 위 정본 규약을 입력 계약으로 사용한다.


---


## B) Inventory State (개념)

Inventory 상태는 "통화", "아이템", "영웅", "Treasure"로 분리된다.

### B-1) Wallet

- key: `currencyType` (=`RewardData.id` when `type=REWARD_TYPE.CURRENCY`)
- value: `amount (long)`

### B-2) Equips

- key: `itemUid` (string, GUID, 인스턴스별 고유 pk)
- value: `AbilityEquip`

`AbilityEquip` 필드 (구현: [12-game-ability](../../../../devian/21-domain-game/12-game-ability/SKILL.md)):
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

`AbilityCard` 필드 (구현: [12-game-ability](../../../../devian/21-domain-game/12-game-ability/SKILL.md)):
- `CardId: string` (== key, `mTable.CardId`)
- `Amount: int` (= `this[STAT_TYPE.CARD_AMOUNT]`)
- 능력치: `AbilityCard : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 수량 = `STAT_TYPE.CARD_AMOUNT`
  - 레벨 = `STAT_TYPE.CARD_LEVEL`
  - Reward/Purchase grants에서는 `STAT_TYPE.CARD_AMOUNT`만 변경된다

### B-4) Heroes

- key: `heroId` (=`RewardData.id` when `type=REWARD_TYPE.HERO`, pk)
- value: `AbilityUnitHero`

`AbilityUnitHero` 필드 (구현: [12-game-ability](../../../../devian/21-domain-game/12-game-ability/SKILL.md)):
- `UnitId: string` (== key, `mTable.UnitId`)
- 수량 = `STAT_TYPE.UNIT_AMOUNT` (Reward grants에서 변경되는 유일한 stat)
- 능력치: `AbilityUnitHero : AbilityUnitBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)


### B-5) Treasure

- `TreasureCurrent` — `InventoryTreasureCurrent` (exp/level 묶음, 단일 인스턴스)
  - `Exp: int` (현재 treasure exp, 기본값 0)
  - `Level: int` (현재 treasure reward level, 기본값 1)
- `TreasureCounts` — `Dictionary<TREASURE_GRADE_TYPE, int>` (grade별 보유 chest count)
  - key: `TREASURE_GRADE_TYPE` (NONE 제외)
  - value: `int` (보유량, 기본값 0)

NOTE:
- `TREASURE_GRADE_TYPE.NONE`은 저장 대상이 아니다.
- chest count와 exp는 음수가 될 수 없다 (0 이하로 clamp).
- current 상태는 grade별로 분리하지 않는다. 단일 `TreasureCurrent.Exp` / `TreasureCurrent.Level`만 사용한다.
- max level 판단은 storage가 아니라 `TREASURE_CHEST` 테이블을 기준으로 한다 (TreasureManager 담당).
- `RewardData.Id`는 `TREASURE_GRADE_TYPE` enum name 문자열이다 (예: `"COMMON"`, `"EPIC"`).
- Treasure 상태의 세부 규칙(max level 판단 등)은 Treasure를 소비하는 상위 시스템이 담당한다.


---


## C) Apply Rules (정본)

### C-1) 공통

- InventoryManager는 타입별 Apply/Revoke/Query API를 제공한다 (예: `ApplyCurrency`, `RevokeCurrency`, `GetCurrencyAmount`).
- 각 API의 반환 타입은 `CommonResult`다.
- RewardData 해석·검증·원자성 보장은 `RewardManager`가 담당한다 ([49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)).
- InventoryManager는 RewardData를 직접 참조하지 않는다.

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


### C-5) `type == REWARD_TYPE.TREASURE`

- `_storage.AddTreasure(gradeType, amount)`
- `gradeType`는 `TREASURE_GRADE_TYPE`으로 파싱한다 (`RewardData.Id` = enum name 문자열).
- `gradeType == TREASURE_GRADE_TYPE.NONE`이면 invalid.
- 없는 grade key는 0에서 시작하여 amount를 누적한다.

### C-5a) `RevokeTreasure`

- 검증: `_storage.GetTreasureCount(gradeType) >= amount` (부족하면 `INVENTORY_REFUND_INSUFFICIENT`).
- 적용: `_storage.SetTreasureCount(gradeType, current - amount)`.

NOTE: `RewardManager.RevokeRewardDatas` / `RevokeRewardDatasPartial`가 RewardData 단위의 회수 원자성을 담당한다. InventoryManager의 Revoke API는 단일 타입/단일 건의 storage 변경만 수행한다.


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
> 직렬화 책임은 [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)의 **`SaveDataJsonCodec`**가 담당한다.
> 아래 스키마는 SaveData JSON의 `"inventory"` 섹션에 해당한다.

Inventory 직렬화 스키마 정본 (SaveData JSON inventory 섹션).

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
- 역직렬화 시 테이블 참조: `TB_ITEM_EQUIP.Get`/`TB_ITEM_CARD.Get`/`TB_UNIT_HERO.Get`으로 `mTable` 복원.
- 역직렬화 순서: wallet → equipments → cards → heroes (heroes 마지막: equip 슬롯 참조 필요).
- Treasure 상태는 별도 root key `"treasure"`로 직렬화된다 (`SaveDataJsonCodecTreasure` 담당).
  - `"treasure"` 스키마: `{ "schemaVersion": 1, "current": { "exp": int, "level": int }, "chestCounts": { "<GRADE_TYPE>": int } }`


---


## E) Error Code Source (정본)

- InventoryManager 타입별 API 실패 에러 코드는 `COMMON_ERROR_TYPE`을 사용한다.
- inventory 전용 에러 코드는 `COMMON_ERROR`을 SSOT로 추가/관리한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `COMMON_ERROR`


---


## F) Inventory Message Trigger (정본)

- Inventory 변경 알림 key는 `INVENTORY_MESSAGE_TYPE` enum을 사용한다.
  - 입력 파일: `input/Domains/Game/ENUM_META.json`
- Trigger 소유자: `InventoryManager`
- 외부 노출: `InventoryManager` helper(`Subcribe/UnSubcribe`)만 허용
- Trigger 직접 참조 금지

현재 최소 메시지:

- `PASS_CHANGED`
  - payload: `args[0] = passId(string)`, `args[1] = owned(bool)`

사용 목적:

- `ACHIEVE_PASS.reqPassId` 조건이 있는 runtime의 `WAIT -> ACTIVE` 전이를 위해 Pass 변동을 구독한다.
