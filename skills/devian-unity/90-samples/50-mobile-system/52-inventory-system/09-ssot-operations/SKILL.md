# 09-ssot-operations — 52-inventory-system


Status: ACTIVE
AppliesTo: v10


이 문서는 Inventory System의 운영/테스트/DoD 정본이다.
SSOT 규칙은 [03-ssot](../03-ssot/SKILL.md)가 정본이다.


---


## 운영 시나리오(개념)


### 1) 앱 시작

- 저장 로드(상위 조립) 후 Inventory를 초기화한다.
- 초기화 순서/저장 스키마는 구현 단계에서 확정한다(NEEDS CHECK).


### 2) 보상 적용(Delta Apply)

- `AddRewards(RewardData[]) -> CommonResult`를 호출한다.
- 실패(`IsFailure`)면 상태는 변경되지 않는다(원자성).
- 성공(`IsSuccess`)이면 Apply 후 변경 이벤트를 발생시키고, 필요 시 상위 조립에서 저장을 트리거한다.


### 3) 조회/UI 갱신

- UI는 조회 API(개념)를 통해 현재 값을 반영한다.
  - 통화: `type=RewardType.Currency` + `currencyType` 잔고
  - 장비: `type=RewardType.Equip` + `equipId(pk)` 스택 수량
  - 카드: `type=RewardType.Card` + `cardId(pk)` 수량
- 아이템 `options`(업그레이드/레벨 등) 표시는 별도 조회/수정 경로가 필요하다(NEEDS CHECK).
- 변경 이벤트 기반 최적화는 구현 단계에서 확정한다(NEEDS CHECK).


---


## 테스트 체크리스트(문서)

- 동일 입력 Delta 적용이 결정적이다(동일 입력 → 동일 결과)
- Apply는 멱등이 아니다(중복 호출 시 중복 반영됨)
- invalid delta 포함 입력은 `CommonResult.Failure`를 반환한다
- invalid delta 포함 입력은 전체 미적용(호출 전/후 상태 동일)이다
- `rewards.Length == 0` 입력은 `CommonResult.Ok()` no-op이다
- 컨텐츠 테이블/enum을 참조하지 않는다
- SaveDataManager↔InventoryManager 상호 비의존이 Policy/InventoryManager 문서에 명시되어 있다
- (NEEDS CHECK) 저장/로드 경계에서 값이 일관된다


---


## DoD

Hard (반드시 0)
- RewardData 정본이 [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)에 명시되고, 52 문서는 이를 참조함
- 컨텐츠 미의존(테이블/enum 직접 참조 금지)이 Policy에 명시됨
- `AddRewards` 반환 타입이 `CommonResult`로 문서에 명시됨
- invalid 입력 시 실패 + 전체 미적용(원자성) 규약이 문서에 명시됨
- `ERROR_COMMON` 기반 `CommonErrorType` 사용 규약이 문서에 명시됨
- 10-inventory-manager 문서가 존재하고 링크됨
