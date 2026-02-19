# 03-ssot — 52-inventory-system (SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

- InventoryDelta 스키마
- 인벤토리 상태 표현(개념)
- Delta 적용 규칙(개념)


---


## A) InventoryDelta (정본)

InventoryDelta는 아래 3필드로 고정한다.

- `type`: `item` | `currency`
- `id`: `string`
- `amount`: `long` (`>= 0`만 허용)

`id` 의미:
- `type=item`이면 `itemId`
- `type=currency`이면 `currencyType`


---


## B) Inventory State (개념)

Inventory 상태는 "통화"와 "아이템"으로 분리된다.

### B-1) CurrencyBalances

- key: `currencyType` (=`InventoryDelta.id` when `type=currency`)
- value: `amount (long)`

### B-2) Items

- key: `itemId` (=`InventoryDelta.id` when `type=item`, pk)
- value: `InventoryItem` (개념)

`InventoryItem`(개념) 최소 필드:
- `itemId: string` (== key)
- `amount: long`
- `options: array`
  - Inventory 내부 속성(업그레이드/레벨 등)
  - Reward/Purchase grants에는 포함되지 않는다

NOTE:
- `itemUid`는 없다(사용하지 않는다).


---


## C) Apply Rules (개념)

### C-1) 공통

- `InventoryDelta.amount`는 `>= 0`만 허용한다.
- 차감/소비/회수(환불/철회 포함)는 InventoryDelta로 처리하지 않는다(별도 시스템/경로).
- 에러/무시/클램프 정책은 구현 단계에서 결정한다(NEEDS CHECK).

### C-2) `type == currency`

- `CurrencyBalances[currencyType] += amount`
- 없는 키는 생성된다.

### C-3) `type == item`

- `Items[itemId].amount += amount`
- 없는 키는 생성된다.
  - 이때 `options`는 "기본값(빈/초기 상태)"로 시작한다(정확한 기본값은 구현 단계 NEEDS CHECK).
- Apply는 `options`를 변경하지 않는다(Reward/Purchase grants에 options가 없기 때문).
