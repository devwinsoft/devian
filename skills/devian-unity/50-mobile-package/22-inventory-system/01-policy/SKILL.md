# 22-inventory-system — Policy


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
  - 통화: `currency_type -> amount(long)`
  - 장비: `itemUid(pk, GUID) -> AbilityItemEquip` (능력치/장비 슬롯을 StatType 기반으로 관리)
- 장비 PK는 `itemUid`(GUID)이다. `item_id`는 템플릿 ID(ITEM_EQUIP 테이블 키)로 사용한다.
- 같은 `item_id`에 여러 인스턴스(각각 고유 `itemUid`)가 존재할 수 있다.
- 장비 내부 속성(레벨/장착 등)은 `AbilityItemEquip : AbilityItemBase : AbilityBase` → `mStats[UNIT_STAT_TYPE.X]`로 정규화한다.


### 2) RewardData 규약은 고정이다 (호환성)

RewardData 스키마는 Reward 시스템 문서가 단일 정본이다.

- 정본: [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
- Inventory 문서는 스키마를 재정의하지 않고 참조만 한다.


### 3) Apply는 멱등을 보장하지 않는다

- Inventory는 Apply(적용)만 수행한다.
- 중복 방지/지급 기록/복구는 호출자가 책임진다.


### 4) public Apply 경계의 invalid 입력은 실패(CommonResult) + 전체 미적용(원자성)

- InventoryManager의 타입별 API(ApplyCurrency, RevokeCurrency 등)는 `CommonResult`를 반환한다.
- invalid 입력이면 `CommonResult.Failure(...)`를 반환한다.
- 실패 시 상태 변경은 0이어야 한다(호출 전/후 동일).
- RewardData[] 단위의 원자성(pre-validate → apply 루프)은 `RewardManager`가 담당한다.
- private helper 내부 invariant 위반만 제한적으로 예외를 허용할 수 있다.
- 단, 외부 입력 검증/table lookup 실패를 public API에서 throw 기본 모델로 바꾸지 않는다.


### 5) 차감/소비/회수는 RewardData로 처리하지 않는다

- `RewardData.Amount`의 비음수 규약은 RewardData 정본을 따른다.
- 차감/소비/회수(환불/철회 포함)는 별도 시스템/경로에서 처리한다.


### 6) 에러는 COMMON_ERROR_TYPE(= COMMON_ERROR SSOT)로 관리한다

- Apply 실패는 `CommonError(COMMON_ERROR_TYPE, message, details)`로 표준화한다.
- inventory 전용 코드는 `COMMON_ERROR`에 추가 후 생성 파이프라인으로 반영한다.
  - 파일: `input/Domains/Common/CommonTable.xlsx`
  - 시트: `COMMON_ERROR`
- `COMMON_ERROR`는 append-only로 유지한다. 기존 row 재정렬/rename 금지.
- 새 코드는 prefix taxonomy(`COMMON_*`, `ABILITY_*`, `INVENTORY_*`, ...)를 따른다.
- private helper 예외를 대체하기 위한 코드 남발은 금지한다.


### 7) InventoryManager는 저장 시스템을 직접 참조하지 않는다

- InventoryManager는 저장 시스템을 직접 참조하지 않는다.
- 저장/로드 결합은 상위 조립(bootstrap/composition root)에서만 수행한다.


### 8) Inventory Trigger는 Manager helper로만 노출한다

- `InventoryMessageTrigger` 직접 참조/직접 주입을 금지한다.
- 외부 시스템은 `InventoryManager`가 제공하는 구독/해제 helper만 사용한다.
- Inventory 상태 변경 publish(`Notify`)는 `InventoryManager` 내부에서만 수행한다.


---


## NEEDS CHECK (구현 단계 결정)

- 최대치/오버플로/클램프 정책
- 스레드/메인스레드 제약(Unity 메인스레드 강제 여부)
- 저장 시점/트리거(저장/로드 결합은 상위 조립에서 수행)
