# 10-mission-manager


MissionManager(설계)는 `CompoSingleton<MissionManager>` 기반으로 `MISSION_*` 테이블 미션을 평가하고, 완료/클레임 상태를 관리한다.
daily 기간 키는 **`MissionManager.Storage.dailyMissionStartUtcMs` + 현재 추정 서버 시각** 기준으로 계산한다.
achievement도 동일한 클레임 흐름을 사용하지만, period reset은 없다.
Firebase는 시계 역할만 하며, mission 정보는 저장하지 않는다.
mission row의 `rewardGroupId`는 컨텐츠 레이어의 reward 키(`49-reward-system`의 `rewardGroupId`)를 사용하며, 실제 지급 실행(Apply)은 RewardManager에 위임한다(구현은 이후).
MissionManager는 `MissionTriggerSystem`과 `MissionRuntimeBase` 계열 runtime을 소유한다.
각 concrete runtime은 자신의 `MISSION_CONDITION_TYPE` trigger를 직접 구독하고 진행도를 갱신한다.
미션 평가는 `conditionType + conditionOp + conditionValue` 조합으로 해석한다.
`mTriggerSystem`은 field initializer에서 즉시 생성하며 null/optional 접근을 허용하지 않는다.


---


## Responsibilities (정본)

- `MISSION_*` 테이블 로드
- `MissionTriggerSystem` 소유
- `MissionRuntimeBase` 계열 runtime 생성/소유/재구축
- `MissionClockSnapshot` 로드/갱신
- `MissionManager.Storage.dailyMissionStartUtcMs` 초기화/보존
- runtime 콜백 기반 조건 평가/진행도 저장
- 런타임 상태(`ACTIVE`, `CLAIMABLE`, `COMPLETED`) 계산
- 서버 시간 기준 daily period 전환 처리
- 클레임 요청 시 `grantId` 생성 + `rewardGroupId` 조회 후 RewardManager(`ApplyRewardGroup`)로 지급 실행 위임
- `grantId` 단위 local claim record 관리
- claim 직후 local/cloud save 실행


---


## Dependencies (개념)

- Reward 지급: `49-reward-system` (RewardManager)
- 로컬 저장: `21-savedata-system/10-savedata-manager`
- 로컬 메시지 라우팅: `10-foundation/22-message-system`
- 서버 clock authority: backend(Firebase Functions 등)
- (선택) 플랫폼 업적/리더보드 연동: `50-leaderboard`


권장 backend callable:
- `getMissionClock`


---


## Public API (설계)

- `InitializeAsync(ct)`
    - 로그인 성공 이후 호출한다. 내부에서 `getMissionClock`을 호출한다. 첫 `getMissionClock` 실패 시 초기화 실패를 반환한다. 테이블 로드 + 저장 상태 로드 + `MissionClockSnapshot` 복원/갱신 + 필요 시 daily anchor 초기화/재설정 + 현재 period key 결정 + 필요한 MissionRuntime 생성/복구를 수행한다
- `RefreshClockAsync(ct)`
    - backend `getMissionClock`에서 최신 `MissionClockSnapshot` 갱신
    - `BaseBootstrap.OnEnterForeground()` 같은 외부 lifecycle hook은 raw callable 대신 이 API를 호출한다
- `TryGetServerNowUtcMs(out serverNowUtcMs)`
    - cached snapshot이 있으면 현재 서버 시각 추정값 반환
- `triggerSystem`
    - 타입은 `MissionTriggerSystem`
    - 다른 시스템은 이 인스턴스로 Mission trigger를 발행한다
- `GetMissionRuntimeState(missionKind, missionId)`
    - `ACTIVE` / `CLAIMABLE` / `COMPLETED`를 계산한다
    - `isActive == false` row는 런타임 상태 대상이 아니다
- `ClaimAsync(missionKind, missionId, ct)`
    - `missionKind`의 타입은 `MISSION_TYPE`이다.
    - 같은 missionId가 daily/achievement 간 재사용될 수 있기 때문에 필수다.
    - 흐름:
      1. achievement면 현재 활성 runtime의 level을 내부에서 결정한다
      2. 현재 period의 `grantId` 생성
      3. timed mission이면 `MissionManager.Storage.dailyMissionStartUtcMs` + 현재 추정 서버 시각 기준 period key 확인
      4. local claim record 중복 체크
      5. `rewardGroupId` 확보 후 RewardManager로 지급 실행 위임
      6. local apply 성공 후 local claim record 저장
      7. achievement면 현재 활성 runtime 기준으로 다음 level row 존재 여부를 확인한다
      8. 다음 level row가 있으면 같은 runtime을 level up 한다
      9. 다음 level row가 없으면 현재 achievement runtime은 `COMPLETED` 상태로 유지한다
      10. `SaveDataManager`로 local save를 즉시 시도하고, 이어서 cloud save도 시도
      11. local save 실패 시 에러를 표시하고 플레이 불가능 상태로 전환한다(TODO)
    - achievement level up 내부 순서:
      1. 현재 `progressValue`를 다음 level `startValue`로 잡는다
      2. 기존 trigger 구독을 해지한다
      3. 같은 `missionUid` runtime의 `level`, `startValue`, `isCompleted`를 갱신한다
      4. 다음 row 기준 condition/reward 바인딩으로 교체한다
      5. 새 condition trigger로 다시 구독한다
- `PruneExpiredMissionState()`
    - 이전 daily key 데이터 지연 정리


---


## Outputs (설계)

- `OnMissionCompleted(missionKind, missionId, level)` (선택)
- `OnMissionClaimable(missionKind, missionId, level)` (선택)
- `OnMissionPeriodChanged(missionKind, oldPeriodKey, newPeriodKey)` (선택)
- UI는 저장 상태 + 테이블을 기반으로 렌더링한다(구현은 이후).


---


## Recommended Internal State

저장 구조 정본은 [12-mission-storage](../12-mission-storage/SKILL.md)를 따른다.

- `MissionStorage`
  - `dailyMissionStartUtcMs`
  - `clockSnapshot`
  - `clockReceivedAtClientUtcMs`
  - `nextMissionUid`
  - `runtimes[missionUid]`
  - `claimRecords[grantId]`
- `MissionRuntimeBase`
  - `missionUid` 단위 runtime 객체
  - 생성 = mission start
  - `missionUid`는 MissionManager가 발급한 `int`
  - `periodKey` / `progressValue` / `isCompleted` 보유
  - daily/achievement 구독 규칙 차이는 concrete subclass override로 구현한다
- `MissionRuntimeDaily`
  - daily 전용 runtime
  - `ACTIVE` 상태에서만 `MissionTriggerSystem` 직접 구독
- `MissionRuntimeAchieve`
  - achievement 전용 runtime
  - `level` / `startValue` 보유
  - 삭제/파기 전까지 `MissionTriggerSystem` 구독을 유지한다
- `MissionTriggerSystem`
  - `MessageSystem<int, MISSION_CONDITION_TYPE>` 특화 인스턴스
  - MissionManager가 단일 소유
- `MissionClaimRecord`
  - `claimedAtClientUtcMs`
  - `rewardGroupId`
- `MissionDefinitionIndex`
  - daily: `missionKind + missionId -> row`
  - achievement: `missionKind + missionId + level -> row`
  - row 조회 용도

정본 방향:
- progress/completion은 `missionUid`의 MissionRuntime을 기준으로 계산한다
- local duplicate 방지는 `grantId` claim record
- trigger-driven progress는 concrete runtime 내부 로직에서 기록한다
- MissionManager는 trigger를 직접 처리하지 않고, MissionRuntime의 onChanged/onCompleted 콜백만 수신한다
- MissionManager는 `nextMissionUid++`를 기본으로 사용하되, 현재 `runtimes`에 이미 존재하는 UID는 건너뛰고 다음 빈 UID를 발급한다
- MissionRuntime이 목표값에 도달하면 MissionManager는 해당 runtime을 `CLAIMABLE`로 본다
- achievement level up 시점은 progress 도달이 아니라 achievement `ClaimAsync` 성공 시점이다
- achievement `ClaimAsync`는 현재 활성 runtime을 내부에서 찾고, 다음 level row가 있으면 같은 runtime의 level/state를 갱신한다
- achievement restore는 저장된 runtime의 `level` / `progressValue` / `isCompleted`를 그대로 사용한다
- achievement level up은 같은 `missionUid` runtime mutation이며, 새 UID를 발급하지 않는다

연관:
- MissionRuntime 정본은 [13-mission-runtime](../13-mission-runtime/SKILL.md)를 따른다.
- MissionRuntimeFactory 정본은 [14-mission-factory](../14-mission-factory/SKILL.md)를 따른다.


---


## Backend Flow (권장)

1. `InitializeAsync`에서 `getMissionClock`
2. `BaseBootstrap.OnEnterForeground()` 같은 resume hook은 `MissionManager.RefreshClockAsync()`를 호출
3. 첫 login에서 `getMissionClock` 실패 시 MissionManager 초기화 실패를 반환하고 login 실패로 처리
4. `MissionManager.Storage.dailyMissionStartUtcMs`가 없으면 첫 sync 시점의 `serverNowUtcMs`로 초기화
5. 현재 sync 시각과 `dailyMissionStartUtcMs`의 차이가 7일 초과면 현재 `serverNowUtcMs`를 새 `dailyMissionStartUtcMs`로 사용하고 daily를 재초기화
6. MissionManager가 current `periodKey` 계산
7. `ClaimAsync` 호출 시 local claim record 중복 검사
8. RewardManager가 `ApplyRewardGroup(rewardGroupId)` 실행
9. local apply 성공 후 local claim record 저장
10. `SaveDataManager`가 local save를 즉시 시도하고, 이어서 cloud save도 시도
11. local save 실패 시 fatal error로 처리
