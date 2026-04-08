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

Inventory 상태는 "통화", "아이템(장비/카드/재료)", "영웅", "Treasure"로 분리된다.

### B-1) Wallet

- key: `currency_type` (=`RewardData.id` when `type=REWARD_TYPE.CURRENCY`)
- value: `amount (long)`

### B-2) Equips

- key: `itemUid` (string, GUID, 인스턴스별 고유 pk)
- value: `AbilityItemEquip`

`AbilityItemEquip` 필드 (구현: [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md)):
- `ItemUid: string` (== key, 인스턴스 고유 ID, GUID)
- `ItemId: string` (템플릿 ID, `mTable.item_id`)
- `OwnerUnitId: string` (장착된 영웅 ItemId, 미장착 시 empty)
- `OwnerSlotType: SLOT_TYPE` (장착 슬롯 enum, `NONE` = 미장착)
- `IsEquipped: bool` (= `OwnerSlotType != SLOT_TYPE.NONE`)
- 능력치: `AbilityItemEquip : AbilityItemBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 레벨 = `STAT_TYPE.ITEM_LEVEL`

NOTE:
- 같은 `item_id`에 여러 인스턴스(각각 고유 `itemUid`)가 존재할 수 있다.
- `RewardData.Id`는 `item_id`(템플릿 ID)이다. `itemUid`는 InventoryManager가 Apply 시 생성한다.
- `ItemData` 클래스는 `AbilityItemEquip`에 통합되어 삭제되었다.
- `InventoryManager.EquippedItems` / `UnequippedItems`는 이 equip SSOT에서 파생된 view다. 별도 저장 정본이 아니다.

### B-3) Cards

- key: `item_id` (=`RewardData.id` when `type=REWARD_TYPE.CARD`, pk)
- value: `AbilityItemCard`

`AbilityItemCard` 필드 (구현: [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md)):
- `ItemId: string` (== key, `mTable.item_id`)
- `Amount: int` (= `this[STAT_TYPE.ITEM_AMOUNT]`)
- `ItemLevel: int` (= `this[STAT_TYPE.ITEM_LEVEL]`)
- 능력치: `AbilityItemCard : AbilityItemBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 수량 = `STAT_TYPE.ITEM_AMOUNT`
  - 레벨 = `STAT_TYPE.ITEM_LEVEL`
  - Reward/Purchase grants에서는 `STAT_TYPE.ITEM_AMOUNT`만 변경된다
  - level up은 현재 `ITEM_CARD_LEVEL` row stat을 제거한 뒤 다음 level row stat을 적용한다

### B-4) Heroes

- key: `item_id` (=`RewardData.id` when `type=REWARD_TYPE.HERO`, pk)
- value: `AbilityItemHero`

`AbilityItemHero` 필드 (구현: [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md)):
- `ItemId: string` (== key, `mTable.item_id`)
- `Amount: int` (= `this[STAT_TYPE.ITEM_AMOUNT]`)
- `ItemLevel: int` (= `this[STAT_TYPE.ITEM_LEVEL]`)
- `Equips: Dict<SLOT_TYPE, AbilityItemEquip>` (outgame 슬롯별 장착 상태)
- 능력치: `AbilityItemHero : AbilityItemBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - level up은 현재 `ITEM_HERO_LEVEL` row stat을 제거한 뒤 다음 level row stat을 적용한다

### B-5) Materials

- key: `item_id` (=`RewardData.id` when `type=REWARD_TYPE.MATERIAL`, pk)
- value: `AbilityItemMaterial`

`AbilityItemMaterial` 필드 (구현: [12-game-ability](../../../21-game-package/12-game-ability/SKILL.md)):
- `ItemId: string` (== key, `mTable.item_id`)
- `Amount: int` (= `this[STAT_TYPE.ITEM_AMOUNT]`)
- `ItemLevel: int` (= `this[STAT_TYPE.ITEM_LEVEL]`)
- 능력치: `AbilityItemMaterial : AbilityItemBase : AbilityBase` → `mStats[STAT_TYPE.X]` (STAT_TYPE 기반 정규화)
  - 수량 = `STAT_TYPE.ITEM_AMOUNT`
  - 레벨 = `STAT_TYPE.ITEM_LEVEL`
  - Reward/Purchase grants에서는 `STAT_TYPE.ITEM_AMOUNT`만 변경된다
  - 현재 `ITEM_MATERIAL_LEVEL` 테이블이 없으므로 level up 대상이 아니다


### B-6) Treasure

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
- 각 API의 반환 타입은 `GameResult`다.
- RewardData 해석·검증·원자성 보장은 `RewardManager`가 담당한다 ([49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)).
- InventoryManager는 RewardData를 직접 참조하지 않는다.
- item ability 생성은 `AbilityItemFactory`를 통해 수행하며, lookup/input 실패는 `GameResult.Failure(...)`로 surface 한다.

### C-2) `type == REWARD_TYPE.CURRENCY`

- `_storage.AddCurrency(currency_type, amount)`
- 없는 키는 생성된다.

### C-3) `type == REWARD_TYPE.EQUIP`

- 매 Apply마다 새 `itemUid`(GUID)를 생성하여 새 AbilityItemEquip 인스턴스를 추가한다.
- `_storage.AddEquip(itemUid, ability)`로 생성된다.
  - 새 AbilityItemEquip의 모든 stat은 0(기본값)으로 시작한다.
- `amount`는 무시한다 (항상 1개 인스턴스 생성).

### C-5) `type == REWARD_TYPE.CARD`

- `InventoryManager.AddCardAmount(item_id, delta)`가 카드 수량 signed delta boundary다.
- 내부 적용은 `_storage.Cards[item_id].AddAmount(delta)` (= `AddStat(STAT_TYPE.ITEM_AMOUNT, delta)`)
- 없는 키는 `_storage.AddCard(item_id, ability)`로 생성된다.
  - 새 AbilityItemCard의 모든 stat은 0(기본값)으로 시작한다.
- Apply는 `STAT_TYPE.ITEM_AMOUNT`만 변경한다 (다른 stat은 보존).
- 수량이 0이 되면 카드 runtime은 storage에서 제거된다.
- 카드 level up은 `InventoryManager.LevelUpCard(item_id)`가 담당한다. 현재 level row stat을 subtract하고 다음 level row stat을 add한다.

### C-4) `type == REWARD_TYPE.HERO`

- `InventoryManager.AddHeroAmount(item_id, delta)`가 영웅 수량 signed delta boundary다.
- 내부 적용은 `_storage.Heroes[item_id].AddStat(STAT_TYPE.ITEM_AMOUNT, delta)`
- 없는 키는 `_storage.AddHero(item_id, ability)`로 생성된다.
  - 새 AbilityItemHero는 `TB_ITEM_HERO.Get(item_id)`로 Init한다.
- Apply는 `STAT_TYPE.ITEM_AMOUNT`만 변경한다 (다른 stat은 보존).
- 수량이 0이 되면 영웅 runtime은 storage에서 제거되고, 장착 중인 equip owner metadata도 함께 정리된다.
- 영웅 level up은 `InventoryManager.LevelUpHero(item_id)`가 담당한다. 현재 level row stat을 subtract하고 다음 level row stat을 add한다.

### C-4a) `type == REWARD_TYPE.MATERIAL`

- `InventoryManager.AddMaterialAmount(item_id, delta)`가 재료 수량 signed delta boundary다.
- 내부 적용은 `_storage.Materials[item_id].AddAmount(delta)` (= `AddStat(STAT_TYPE.ITEM_AMOUNT, delta)`)
- 없는 키는 `_storage.AddMaterial(item_id, ability)`로 생성된다.
  - 새 AbilityItemMaterial은 `TB_ITEM_MATERIAL.Get(item_id)`로 Init한다.
- Apply는 `STAT_TYPE.ITEM_AMOUNT`만 변경한다 (다른 stat은 보존).
- 수량이 0이 되면 재료 runtime은 storage에서 제거된다.
- 재료는 현재 level table이 없으므로 `LevelUpMaterial`을 두지 않는다.


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

장비 장착/해제의 저장 모델은 `AbilityItemHero._SetEquip()` / `AbilityItemHero._RemoveEquip()`이다.
이 메서드는 hero loadout metadata와 equip owner metadata만 관리한다.
실제 장비 stat 계산은 `AbilityUnitHero._Equip()` / `AbilityUnitHero._Unequip()`이 담당한다.
InventoryStorage는 `Equip(heroId, equipSlot, equipUid)` / `Unequip(heroId, equipSlot)` 편의 메서드로 item hero 저장 모델에 위임한다.

- slot rule 정본은 `ITEM_EQUIP.equip_type` + `EQUIP_SLOT.allowed_slots/two_handed`다.
- `SLOT_TYPE.NONE`은 저장/장착 대상이 아니다.
- `two_handed=true` 장비를 `HAND_MAIN`에 장착하면 현재 `HAND_SUB` 장비는 자동 해제된다.
- `HAND_MAIN`에 양손 장비가 있는 상태에서 `HAND_SUB` 장착 시도는 실패한다.

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
      "item_id": "<string>",
      "itemUid": "<string>",
      "item_level": <int>
    }
  },
  "cards": {
    "<item_id>": {
      "item_id": "<string>",
      "item_level": <int>,
      "amount": <int>
    }
  },
  "materials": {
    "<item_id>": {
      "item_id": "<string>",
      "amount": <int>
    }
  },
  "heroes": {
    "<item_id>": {
      "item_id": "<string>",
      "item_level": <int>,
      "amount": <int>,
      "equips": { "<slotNumber>": "<equipUid>" }
    }
  }
}
```

- Hero equips: slotNumber(string key) → equipUid(string value).
- hero equip ownership의 저장 SSOT는 `heroes[*].equips`다.
- `equipments[*]`는 owner 정보를 저장하지 않는다. equip owner는 deserialize 시 hero equip 맵으로만 복원한다.
- 역직렬화 시 테이블 참조: `TB_ITEM_EQUIP.Get`/`TB_ITEM_CARD.Get`/`TB_ITEM_MATERIAL.Get`/`TB_ITEM_HERO.Get`으로 `mTable` 복원.
- 역직렬화 순서: wallet → equipments(항상 unequipped 생성) → cards → materials → heroes (heroes 마지막: equip 슬롯 참조 필요).
- hero equip 맵에 중복 `equipUid`가 나오면 load 실패로 처리한다.
- legacy save의 `CARD_AMOUNT`/`UNIT_AMOUNT`/`CARD_LEVEL`/`UNIT_LEVEL` key는 load 시 `ITEM_AMOUNT`/`ITEM_LEVEL`로 매핑한다.
- 역직렬화 실패는 `CommonResult.Failure(...)`로 즉시 반환하며, 부분 복원 상태를 성공으로 취급하지 않는다.
- Treasure 상태는 별도 root key `"treasure"`로 직렬화된다 (`SaveDataJsonCodecTreasure` 담당).
  - `"treasure"` 스키마: `{ "schemaVersion": 1, "current": { "exp": int, "level": int }, "chestCounts": { "<GRADE_TYPE>": int } }`


---


## E) Error Code Source (정본)

- InventoryManager 타입별 API 실패 에러 코드는 `GAME_ERROR_TYPE`을 사용한다.
  - `GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT` — null/empty/음수 등 입력 검증 실패
  - `GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT` — 회수 시 잔고/수량 부족


---


## F) Inventory Message Trigger (정본)

- Inventory 변경 알림 key는 `INVENTORY_MESSAGE_TYPE` enum을 사용한다.
  - 입력 파일: `input/Domains/Game/ENUM_INVENTORY.json`
- Trigger 소유자: `InventoryManager`
- 외부 노출: `InventoryManager` helper(`Subcribe/UnSubcribe`)만 허용
- Trigger 직접 참조 금지

현재 메시지:

- `PASS_OWNERSHIP_CHANGED`
  - payload: `args[0] = passId(string)`, `args[1] = owned(bool)`
- `CURRENCY_CHANGED`
  - payload: `args[0] = currencyType(CURRENCY_TYPE)`, `args[1] = delta(long)`, `args[2] = currentAmount(long)`
- `ITEM_EQUIP_CHANGED`
  - payload: `args[0] = itemUid(string)`, `args[1] = itemId(string)`, `args[2] = runtime(AbilityItemEquip)`
- `ITEM_CARD_CHANGED`
  - payload: `args[0] = itemId(string)`, `args[1] = runtime(AbilityItemCard)`
- `ITEM_MATERIAL_CHANGED`
  - payload: `args[0] = itemId(string)`, `args[1] = runtime(AbilityItemMaterial)`
- `ITEM_HERO_CHANGED`
  - payload: `args[0] = itemId(string)`, `args[1] = runtime(AbilityItemHero)`
- `ITEM_EQUIP_LIST_CHANGED`
  - payload: `args[0] = action(INVENTORY_LIST_CHANGE_TYPE)`, `args[1] = itemUid(string)`, `args[2] = itemId(string)`, `args[3] = runtimeOrNull(AbilityItemEquip)`
- `ITEM_CARD_LIST_CHANGED`
  - payload: `args[0] = action(INVENTORY_LIST_CHANGE_TYPE)`, `args[1] = itemId(string)`, `args[2] = runtimeOrNull(AbilityItemCard)`
- `ITEM_MATERIAL_LIST_CHANGED`
  - payload: `args[0] = action(INVENTORY_LIST_CHANGE_TYPE)`, `args[1] = itemId(string)`, `args[2] = runtimeOrNull(AbilityItemMaterial)`
- `ITEM_HERO_LIST_CHANGED`
  - payload: `args[0] = action(INVENTORY_LIST_CHANGE_TYPE)`, `args[1] = itemId(string)`, `args[2] = runtimeOrNull(AbilityItemHero)`
- `RENTAL_CHANGED`
  - payload: `args[0] = itemId(string)`, `args[1] = expiresAtClientUtcMs(long)`, `args[2] = active(bool)`
- `TREASURE_STATE_CHANGED`
  - payload: `args[0] = gradeType(TREASURE_GRADE_TYPE)`, `args[1] = deltaCount(int)`, `args[2] = currentCount(int)`, `args[3] = currentLevel(int)`, `args[4] = currentExp(int)`
- `INVENTORY_SNAPSHOT_CHANGED`
  - payload: `args[0] = reason(INVENTORY_SNAPSHOT_CHANGE_REASON)`

사용 목적:

- `ACHIEVE_PASS.req_pass_id` 조건이 있는 runtime의 `WAIT -> ACTIVE` 전이를 위해 Pass 변동을 구독한다.
- wallet/item UI는 타입별 `*_CHANGED`를 구독하고 payload 또는 현재 storage 재조회로 갱신한다.
- item 추가/삭제는 `*_LIST_CHANGED`로 별도 발행한다.
- load/import/clear 같은 bulk inventory 변경은 per-item replay 없이 `INVENTORY_SNAPSHOT_CHANGED` 한 번만 발행한다.
