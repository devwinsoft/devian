# 52-inventory-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Inventory System의 모듈 경계/하드룰을 정의한다.


---


## Hard Rules


### 1) Inventory는 컨텐츠를 직접 참조하지 않는다

- 컨텐츠 도메인 테이블/enum/구현체를 직접 참조 금지.
- Inventory는 아래 "시스템 레이어 상태"만 다룬다:
  - 통화: `currencyType -> amount(long)`
  - 아이템: `itemId(pk) -> ItemData` (수량/능력치/장비 슬롯을 StatType 기반으로 관리)
- `itemUid`는 사용하지 않는다(아이템 PK는 `itemId`).
- 아이템 내부 속성(수량/레벨/장착 등)은 `ItemAbility : BaseAbility` → `mStats[StatType.X]`로 정규화한다.


### 2) RewardData 규약은 고정이다 (호환성)

RewardData 스키마는 Reward 시스템 문서가 단일 정본이다.

- 정본: [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
- Inventory 문서는 스키마를 재정의하지 않고 참조만 한다.


### 3) Apply는 멱등을 보장하지 않는다

- Inventory는 Apply(적용)만 수행한다.
- 중복 방지/지급 기록/복구는 호출자(Mission/Purchase)가 책임진다.


### 4) invalid 입력은 실패(CommonResult) + 전체 미적용(원자성)

- `AddRewards`는 `CommonResult`를 반환한다.
- invalid가 하나라도 있으면 `CommonResult.Failure(...)`를 반환한다.
- 실패 시 상태 변경은 0이어야 한다(호출 전/후 동일).
- `rewards.Length == 0`은 valid no-op이며 `CommonResult.Ok()`를 반환한다.


### 5) 차감/소비/회수는 RewardData로 처리하지 않는다

- `RewardData.Amount`의 비음수 규약은 RewardData 정본을 따른다.
- 차감/소비/회수(환불/철회 포함)는 별도 시스템/경로에서 처리한다.


### 6) 에러는 CommonErrorType(= ERROR_COMMON SSOT)로 관리한다

- Apply 실패는 `CommonError(CommonErrorType, message, details)`로 표준화한다.
- inventory 전용 코드는 `ERROR_COMMON`에 추가 후 생성 파이프라인으로 반영한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `ERROR_COMMON`


### 7) SaveDataManager와 InventoryManager는 서로를 모른다

- InventoryManager는 SaveDataManager를 직접 참조하지 않는다.
- SaveDataManager는 InventoryManager/Inventory 스키마를 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.


---


## NEEDS CHECK (구현 단계 결정)

- 최대치/오버플로/클램프 정책
- 스레드/메인스레드 제약(Unity 메인스레드 강제 여부)
- 저장 시점/트리거(저장/로드 결합은 상위 조립에서 수행)
