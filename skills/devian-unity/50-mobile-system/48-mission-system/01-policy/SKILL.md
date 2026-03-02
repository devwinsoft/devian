# 48-mission-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Mission(일일/업적) 시스템의 모듈 경계와 하드룰을 정의한다.


---


## Hard Rules


### 1) 조건 평가는 MissionManager가 책임진다

- `MISSION_*` 테이블의 `MISSION_CONDITION_TYPE` / `MISSION_OP_TYPE`를 해석하고, 완료 여부를 판단하는 것은 MissionManager의 책임이다.
- LeaderboardManager는 플랫폼 업적/리더보드 연동만 담당하며, 일반 미션 조건 평가를 담당하지 않는다.


### 1-1) 미션 진행도 입력은 MissionTriggerSystem으로만 받는다

- MissionManager는 `MessageSystem<int, MISSION_CONDITION_TYPE>` 특화 인스턴스인 `MissionTriggerSystem`을 소유한다.
- MissionManager는 `CompoSingleton<MissionManager>`다.
- `mTriggerSystem`은 field initializer에서 `new MissionTriggerSystem()`으로 즉시 생성한다.
- `MissionManager.triggerSystem` 접근은 optional/null 허용 없이 `Instance.mTriggerSystem`을 사용한다.
- 다른 시스템은 Mission 진행도 입력을 직접 `OnGameEvent` 형태로 호출하지 않고, `MissionTriggerSystem.Notify(...)`로 전달한다.
- daily MissionRuntime은 `ACTIVE`일 때만 자신의 `conditionType`으로 직접 구독을 시작하고, `CLAIMABLE`/`COMPLETED`/삭제 시 구독을 해지한다.
- achievement MissionRuntime은 삭제 전까지 구독을 유지한다.
- `MessageSystem`은 중복 구독을 허용한다. 초기화를 2번 해서 중복 등록되는 것은 caller bug로 본다.
- `MissionTriggerSystem`은 로컬 trigger 라우터일 뿐이며, 큐/재생/영속성 책임은 없다.


### 2) 보상 지급 기록/중복 방지/기간 전환은 MissionManager가 책임진다

- MissionManager는 "완료/클레임" 뿐 아니라, **지급 여부 조회/기록/개별 지급 기록/기간 전환 처리**를 책임진다.
- RewardManager는 **보상 지급 실행(Apply)** 만 수행한다. (멱등/기록/복구 책임 없음)

연관:
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)


### 3) grantId는 Mission 반복 지급을 정확히 표현해야 한다

- 일일 미션은 기간이 바뀌면 같은 missionId라도 다시 지급 가능해야 한다.
- 따라서 MissionManager는 `missionKind + missionId + level? + periodKey` 조합으로 `grantId`를 생성해야 한다.
- `grantId` 규칙 정본은 [03-ssot](../03-ssot/SKILL.md)이다.
- missionId가 카테고리 간 재사용되어도 충돌하면 안 되므로, `grantId`에는 category가 반드시 포함되어야 한다.


### 4) timed mission의 period key는 MissionStorage anchor로 계산한다

- timed mission = `MISSION_TYPE.DAY`
- MissionManager는 첫 로그인 성공 시의 서버 시각을 `MissionManager.Storage.dailyMissionStartUtcMs`로 저장한다.
- 이후에는 backend가 제공한 `MissionClockSnapshot.serverNowUtcMs`로 현재 서버 시각을 추정하고,
  `dailyMissionStartUtcMs`에서 24시간 단위 index를 계산해 `dailyKey`를 만든다.
- 현재 sync 시각과 `dailyMissionStartUtcMs`의 차이가 7일 초과면 현재 서버 시각으로 `dailyMissionStartUtcMs`를 다시 잡는다.
- 디바이스 로컬 시간만으로 anchor를 만들지 않는다.
- 로그인/동기화 시점 이후에는 마지막으로 보정한 서버 시간을 기준으로 클라이언트가 계속 판정한다.


### 5) destructive reset 대신 period-scoped state를 사용한다

- 24시간 경계에서 기존 state를 제자리에서 초기화하지 않는다.
- MissionManager는 현재 period key를 포함한 state/grant key를 사용한다.
- period가 바뀌면 새 key 공간으로 이동하고, 이전 period 데이터는 지연 정리(prune)한다.
- 이유:
  - 경계 시각 race condition 감소
  - claimed/completed/progress 상태 꼬임 방지
  - 일일 전환 처리 단순화


### 5-1) MissionRuntime이 `ProgressValue`를 저장하고, conditionOp가 처리 규칙을 결정한다

- MissionManager는 미션별 `MissionRuntimeBase` 계열 runtime을 관리한다.
- MissionRuntime은 자신이 구독한 trigger를 내부 로직으로 처리한다.
- `MISSION_OP_TYPE.MAX`: `runtime.progressValue = max(runtime.progressValue, msgValue)`로 갱신한다.
- `MISSION_OP_TYPE.SUM`: 누적형 progress다. 실제 누적 방식은 concrete runtime 구현이 결정한다.
  - `MissionRuntimeDaily`: `runtime.progressValue = min(conditionValue, runtime.progressValue + msgValue)`
  - `MissionRuntimeAchieve`: `runtime.progressValue = runtime.progressValue + msgValue`
- `runtime.progressValue >= conditionValue && runtime.isCompleted == false`이면 `CLAIMABLE`이다.
- `COMPLETED`는 수동 claim까지 끝난 상태다.
- achievement는 `ClaimAsync()` 시 보상을 지급하고 다음 level로 전환한다.
- 다음 level row가 있으면 같은 runtime이 level up 한다.
- 마지막 level이면 `COMPLETED`로 유지한다.
- `MISSION_OP_TYPE.NONE`: 아무 기능도 하지 않는다. `isActive=true` row라도 MissionRuntime을 생성하지 않는다.
- daily runtime의 `SUM`은 `progressValue`가 `conditionValue` 값을 넘어설 수 없다.
- achieve runtime의 `SUM`은 `progressValue`가 `conditionValue`를 넘어설 수 있다.
- `ProgressValue`는 저장되는 실제 누적값 이름이다. 계산용 getter 이름이 필요하면 `CurrentProgress`를 별도로 둘 수 있다.


### 6) 저장/복구는 SaveDataManager 규약을 따른다

- 미션 진행도/완료/클레임 상태는 로컬 저장을 전제로 한다.
- 저장 책임의 큰 틀은 `21-savedata-system` 규약을 따른다.
- claim 시에는 `ApplyRewardGroup()` 이후 mission storage mutation을 저장하고, 바로 local/cloud save를 시도해야 한다.
- local save 실패는 매우 심각한 오류이며, 플레이 불가능 상태로 처리한다(TODO).
- 첫 login 시 `getMissionClock`를 못 받으면 MissionManager 초기화 실패로 처리하고, 결과적으로 login 실패로 본다.

연관: [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)


### 7) Firebase는 mission 정보를 저장하지 않는다

- Firebase는 mission의 조건/진행도/클레임 기록을 저장하지 않는다.
- Firebase의 역할은 `getMissionClock`을 통한 서버 시계 제공뿐이다.
- mission 중복 방지는 local claim record와 save 복원에 의존한다.


### 8) backend authority를 명시한다

- timed mission에서 backend authority는 `MissionClockSnapshot` 제공 하나만 둔다.
- MissionManager는 backend가 내려준 현재 시각을 기준으로 period를 계산한다.
- v1 정본은 `클라 apply -> claim record 기록 -> local/cloud save` 순서를 사용한다.


### 9) v1 trust model을 명시한다

- 이번 설계 보완의 우선순위는 **서버 시간 기준 period 판정 + 로컬 지급 후 즉시 저장**이다.
- 일반 gameplay progress(`kill count`, `stage clear count`)는 v1에서 MissionManager가 로컬 평가할 수 있다.
- 그러나 이 경우 "진행도 자체"는 클라이언트 신뢰 모델을 벗어나지 못한다.
- 경제 가치가 큰 미션은 후속 단계에서 서버 이벤트/서버 카운터 기반으로 확장할 수 있어야 한다.
