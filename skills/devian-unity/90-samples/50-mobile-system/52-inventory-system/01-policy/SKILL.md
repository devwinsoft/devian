# 52-inventory-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Inventory System의 모듈 경계/하드룰을 정의한다.


---


## Hard Rules


### 1) Inventory는 컨텐츠를 직접 참조하지 않는다

- Game 도메인 테이블/enum을 직접 참조 금지.
- Inventory는 아래 "시스템 레이어 상태"만 다룬다:
  - 통화: `currencyType -> amount(long)`
  - 아이템: `itemId(pk) -> (amount(long), options)`
- `itemUid`는 사용하지 않는다(아이템 PK는 `itemId`).
- `options`는 Inventory 내부 속성(업그레이드/레벨 등)이며 Reward/Purchase grants에는 포함되지 않는다.


### 2) InventoryDelta 규약은 고정이다 (호환성)

InventoryDelta는 시스템 간 호환을 위해 아래 3필드로 고정한다.

- `type`: `item` | `currency`
  - 문자열 리터럴로 비교한다(컨텐츠(Game) enum/테이블 타입 참조 금지).
- `id`: `string`
  - `type=item`이면 `itemId`
  - `type=currency`이면 `currencyType`
- `amount`: `long` (`>= 0`만 허용)

정본: [03-ssot](../03-ssot/SKILL.md)


### 3) Apply는 멱등을 보장하지 않는다

- Inventory는 Apply(적용)만 수행한다.
- 중복 방지/지급 기록/복구는 호출자(Mission/Purchase)가 책임진다.


### 4) 차감/소비/회수는 InventoryDelta로 처리하지 않는다

- `InventoryDelta.amount`는 `>= 0`만 허용한다.
- 차감/소비/회수(환불/철회 포함)는 별도 시스템/경로에서 처리한다.


### 5) SaveDataManager와 InventoryManager는 서로를 모른다

- InventoryManager는 SaveDataManager를 직접 참조하지 않는다.
- SaveDataManager는 InventoryManager/Inventory 스키마를 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.


---


## NEEDS CHECK (구현 단계 결정)

- 최대치/오버플로/클램프 정책
- 스레드/메인스레드 제약(Unity 메인스레드 강제 여부)
- 저장 시점/트리거(저장/로드 결합은 상위 조립에서 수행)
