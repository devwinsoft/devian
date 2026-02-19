# 10-inventory-manager


Status: ACTIVE
AppliesTo: v10


InventoryManager(설계)는 `InventoryDelta[]` 입력을 받아 인벤토리 상태에 적용(Apply)한다.

- `type=currency`: `currencyType -> amount(long)` 잔고 누적
- `type=item`: `itemId(pk) -> (amount(long), options)` 아이템 스택 누적

`options`는 Inventory 내부 속성(업그레이드/레벨 등)이며 Reward/Purchase grants에는 포함되지 않는다.


---


## Responsibilities (정본)

- `InventoryDelta[]`를 Apply 한다.
  - `type=currency`와 `type=item`의 처리 로직은 분기된다(정본).
- 수량 조회를 제공한다(개념).
  - `type=currency`: 잔고 조회
  - `type=item`: 해당 `itemId(pk)` 스택 수량 조회
- 변경 이벤트를 제공한다(개념).

NOTE:
- `options`는 InventoryItem 내부 속성이다.
- Apply는 `options`를 변경하지 않는다(Reward/Purchase grants에 options가 없기 때문).
- `options` 조회/수정 API는 "업그레이드/레벨업 시스템" 설계에서 확정한다(NEEDS CHECK).

비책임:
- 컨텐츠(Game) 정의를 직접 참조하지 않는다.
- 멱등/기록/복구는 호출자(Mission/Purchase)가 책임진다.


---


## Dependencies (개념)

- InventoryManager는 SaveDataManager를 직접 참조하지 않는다.
- SaveDataManager는 InventoryManager/Inventory 스키마를 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.


---


## Public API (설계)

- `InitializeAsync(ct)`
  - 외부 저장 로드 이후 호출되어 인벤토리 상태를 준비한다(개념).
- `ApplyDeltasAsync(deltas, ct)`
  - `InventoryDelta[]`를 Apply 한다(멱등 아님).
- `GetAmount(type, id)`
  - `(type,id)`에 대한 현재 수량을 반환한다(개념).
- `(선택) OnChanged`
  - 변경 이벤트(개념).


---


## Notes

- 내부 구현 메서드는 Devian 정책에 따라 `_MethodName` 네이밍을 사용한다(NEEDS CHECK: 구현 단계).
