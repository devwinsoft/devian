# 03-ssot — 52-inventory-system (SSOT)


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
- 본 문서(52-inventory-system)는 `RewardData` 필드 스키마를 재정의하지 않는다.
- Inventory는 위 정본 규약을 입력 계약으로 사용한다.


---


## B) Inventory State (개념)

Inventory 상태는 "통화"와 "아이템"으로 분리된다.

### B-1) CurrencyBalances

- key: `currencyType` (=`RewardData.id` when `type=RewardType.Currency`)
- value: `amount (long)`

### B-2) Items

- key: `itemId` (=`RewardData.id` when `type=RewardType.Item`, pk)
- value: `ItemData`

`ItemData` 필드 (구현: [11-inventory-storage](../11-inventory-storage/SKILL.md)):
- `ItemId: string` (== key, `mItemAbility.ItemId`)
- `Amount: int` (= `mItemAbility[StatType.ItemAmount]`)
- 능력치: `ItemAbility : BaseAbility` → `mStats[StatType.X]` (StatType 기반 정규화)
  - 수량 = `StatType.ItemAmount`
  - 장착 슬롯 = `StatType.ItemSlotNumber`
  - 레벨 = `StatType.ItemLevel`
  - Reward/Purchase grants에서는 `StatType.ItemAmount`만 변경된다

NOTE:
- `itemUid`는 없다(사용하지 않는다).
- `InventoryItem` 클래스는 사용하지 않는다 — `ItemData`가 전담한다.


---


## C) Apply Rules (정본)

### C-1) 공통

- `AddRewards`의 반환 타입은 `CommonResult`다.
- 입력 검증은 `RewardData` 정본([49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md))을 따른다.
- `rewards.Length == 0`은 valid no-op이다 (`CommonResult.Ok()`).
- invalid가 하나라도 있으면 `CommonResult.Failure`를 반환하고 전체 미적용(원자성)한다.
- 차감/소비/회수(환불/철회 포함)는 RewardData로 처리하지 않는다(별도 시스템/경로).

### C-2) `type == RewardType.Currency`

- `_storage.AddCurrency(currencyType, amount)`
- 없는 키는 생성된다.

### C-3) `type == RewardType.Item`

- `_storage.BagItems[itemId].AddAmount(amount)` (= `mItemAbility.AddStat(StatType.ItemAmount, amount)`)
- 없는 키는 `_storage.AddItem(itemId, ability)`로 생성된다.
  - 새 ItemData의 모든 stat은 0(기본값)으로 시작한다.
- Apply는 `StatType.ItemAmount`만 변경한다 (다른 stat은 보존).


---


## D) Error Code Source (정본)

- `AddRewards` 실패 에러 코드는 `CommonErrorType`을 사용한다.
- inventory 전용 에러 코드는 `ERROR_COMMON`을 SSOT로 추가/관리한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `ERROR_COMMON`
