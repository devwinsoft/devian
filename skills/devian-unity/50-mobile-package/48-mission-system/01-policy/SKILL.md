# 48-mission-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

Mission(`DAILY`/`PERIOD`) 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) 미션 테이블 분리는 고정이다

- `MISSION_DAILY`는 `missionType`을 가지지 않는다.
- `MISSION_PERIOD`는 `missionType/fixed/orderNum`을 가지지 않는다.
- `MISSION_PERIOD.day`는 `1~7` 정수 범위를 사용한다.
- `MISSION_PERIOD.day`는 period runtime activation group key다(동일 day 동시 활성화).
- 조건 타입/저장 연산/비교 연산 정본은 `GAME_MESSAGE`다.

### 2) 진행도 입력은 `GameMessageTrigger`를 통해 받는다

- Mission runtime은 MissionManager helper를 통해 `GameMessageTrigger`를 직접 구독한다.
- 다른 시스템은 `MissionManager.Notify(statType, value)`만 호출한다.
- MissionRuntime이 별도 gameplay API를 직접 호출받는 구조는 금지한다.

### 3) trigger 처리 순서는 `stats 갱신 -> game trigger publish -> achieve notify`다

- MissionManager.Notify는 `GameMessageManager.Notify`로 위임한다.
- `GameMessageManager`가 같은 `messageType`을 참조하는 `TB_GAME_MESSAGE` row를 순회하며 `message.stats[messageId]`를 먼저 갱신한다.
  - `SUM`: `current + delta`
  - `MAX`: `max(current, delta)`
  - `MIN`: `min(current, delta)` (음수 입력 무시)
- 그 다음 `GameMessageTrigger` publish로 daily runtime 구독자 notify를 수행한다.
- 마지막에 `AchieveManager.Notify`를 호출해 업적 시스템으로 동일 이벤트를 전달한다.

### 4) 진행도 저장 정본은 `runtime.progressValue`다

- `DAILY` runtime이 `progressValue`를 자체 보유/저장한다.
- `PERIOD` runtime이 `progressValue`를 자체 보유/저장한다.
- `MissionStorage.stats`는 `missionStatId` 단위 누적 캐시이며 runtime 상태 정본은 아니다.

### 5) `PERIOD` 활성화/리셋 규칙은 고정이다

- 초기화/리셋 시 `MISSION_PERIOD`의 모든 row runtime을 생성한다.
- 생성 직후 기본 상태는 WAIT다.
- `day == 1`이면 즉시 ACTIVE 전환한다.
- `day == n`이면 `(n - 1)`일 경과 후 ACTIVE 전환한다.
- `PERIOD` cycle은 10일 주기로 리셋한다.

### 6) 저장/복구 및 보상 처리 경계

- MissionManager는 `DAILY`/`PERIOD` claim orchestration만 담당한다.
- Reward 적용은 RewardManager에 위임한다.
- claim 성공 후 저장은 즉시 수행한다.
- 업적(`ACHIEVE`) claim/runtime 책임은 AchieveManager에 있다.

---

## Related

- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
