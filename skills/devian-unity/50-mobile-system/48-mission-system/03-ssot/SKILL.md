# 03-ssot — 48-mission-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

- 컨텐츠 도메인 Mission 테이블(상위 컨텐츠 레이어 SSOT)
- 컨텐츠 도메인 Reward 테이블(상위 컨텐츠 레이어 SSOT)
- `MISSION_DAY` 테이블 스키마 (일일 미션)
- `MISSION_ACHIEVE` 테이블 스키마 (업적 미션)
- `MissionTriggerSystem` 규약
- `MissionMessageSystem` 규약
- `MissionClockSnapshot` / mission daily clock 규약
- grantId 생성 규칙
- missionUid 생성 규칙
- daily 기간 키 계산 규칙(서버 시간 기준)


---


## A) Core Terms (정본)

- `missionId`: daily에서는 내부 표준 미션 ID, achievement에서는 그룹 ID(string)
- `missionType`: `MISSION_TYPE` enum. 값은 `DAY` | `ACHIEVE`
- `conditionType`: `MISSION_CONDITION_TYPE` enum. MissionTriggerSystem에서 어떤 trigger를 받을지 결정한다.
- `conditionOp`: `MISSION_OP_TYPE` enum. runtime의 `ProgressValue`를 어떤 방식으로 갱신할지 결정한다.
- `conditionValue`: 목표값(`CBigInt`). 누적 진행도가 이 값 이상이면 완료다.
- `rewardGroupId`: 보상 키(string). `49-reward-system`의 `rewardGroupId` 정본을 그대로 사용한다. 정본은 `MISSION_DAY` / `MISSION_ACHIEVE` 테이블이며, runtime에는 저장하지 않는다.
- `dailyMissionStartUtcMs`: `MissionManager.Storage.dailyMissionStartUtcMs`. 첫 로그인 성공 시 받은 서버 시각. daily period의 개인별 시작 anchor
- `periodKey`: 반복 지급 구간 식별 키
  - daily: 현재 daily claim/reset 구간 메타데이터인 `day:{dailyPeriodIndex}`
  - achievement: 고정값 `once`
- `MissionRuntimeBase`: `missionUid` 단위 미션 runtime 추상 베이스. 생성 = 해당 미션 definition의 runtime 시작
- `Index`: UI 표시용 0-based runtime 정렬 값
- `MissionScheduler`: Mission runtime의 생성/복구/리바인드/period 정리/uid 발급을 담당하는 내부 scheduler
- `MissionTriggerSystem`: `MessageSystem<int, MISSION_CONDITION_TYPE>` 특화 인스턴스
- `MissionMessageSystem`: `MessageSystem<EntityId, MISSION_MESSAGE>` 특화 인스턴스
- `MissionClockSnapshot`: 서버가 내려주는 "현재 서버 시각" 스냅샷
- `missionUid`: MissionScheduler가 발급하는 runtime 식별용 `int` UID
- `grantId`: `missionType + missionId + level? + periodKey` 기반 지급 기록 키

NOTE:
- 일일/업적은 **테이블을 분리**한다:
  - `MISSION_DAY`
- `MISSION_ACHIEVE`
- `uiGroup` 컬럼은 사용하지 않는다.


---


## B) Table Schema (설계 정본)

### `MISSION_DAY`

| field | type | note |
|------|------|------|
| `missionId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `fixed` | bool | `true`면 daily selection에서 무조건 포함 |
| `orderNum` | int | UI 정렬 기준 값. 1부터 시작 |
| `conditionType` | `MISSION_CONDITION_TYPE` | Mission 조건 타입 enum |
| `conditionOp` | `MISSION_OP_TYPE` | runtime progress 처리 방식 enum |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키(= 컨텐츠 레이어 rewardGroupId) |

### `MISSION_ACHIEVE`

| field | type | note |
|------|------|------|
| `index` | int (pk) | 테이블 row pk |
| `missionId` | string | 업적 그룹 ID |
| `isActive` | bool | 운영 토글 |
| `level` | int | 업적 단계 |
| `orderNum` | int | UI 정렬 기준 값. 1부터 시작. 같은 `missionId` group은 같은 값을 사용 |
| `conditionType` | `MISSION_CONDITION_TYPE` | Mission 조건 타입 enum |
| `conditionOp` | `MISSION_OP_TYPE` | runtime progress 처리 방식 enum |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키(= 컨텐츠 레이어 rewardGroupId) |

NOTE:
- `MISSION_TYPE`은 v1에서 아래 값 집합을 사용한다:
  - `DAY`
  - `ACHIEVE`
- daily는 `missionId`가 실제 미션 식별자다.
- achievement는 `missionId`가 그룹 ID이고, 실제 단계 미션 식별은 `missionId + level`이다.
- `orderNum`은 테이블 authoring 정렬 값이고 1부터 시작한다.
- achievement row의 `missionId + level` 유일성은 data layer에서 보장한다.
- `missionUid`는 runtime pk이고, `missionId + level`은 achievement definition 식별자다.
- `MISSION_TYPE`, `MISSION_CONDITION_TYPE`, `MISSION_OP_TYPE`, `MISSION_MESSAGE`는 `ENUM_MISSION.json`에서 선언한다.
- `MISSION_CONDITION_TYPE`의 v1 값 집합:
  - `NONE`
  - `LOGIN`
  - `STAGE_CLEAR`
  - `ACHIEVEMENT_UNLOCKED`
- `MISSION_OP_TYPE`는 v1에서 아래 값 집합을 사용한다:
  - `NONE`
  - `MAX`
  - `SUM`
- `MISSION_MESSAGE`는 v1에서 아래 값 집합을 사용한다:
  - `NONE`
  - `RUNTIME_INIT`
  - `RUNTIME_PROGRESS`
  - `RUNTIME_CLAIMABLE`
  - `RUNTIME_REWARDED`
  - `DAY_RESET`
  - `ACHIEVE_LEVEL_UP`
- `conditionType + conditionOp + conditionValue` 조합이 미션 판정 입력의 정본이다.
- daily selection은 `fixed`를 먼저 적용한다.
- daily runtime의 `Index`는 최종 선택된 row를 `orderNum ASC`, `missionId ASC`로 정렬한 뒤 0부터 다시 부여한다.
- achieve runtime의 `Index`는 현재 row의 `orderNum - 1`이다.
- `conditionValue`의 authoring plain format은 `{base, pow}`다. 예: `{5.5, 6}`
- `MISSION_CONDITION_TYPE.NONE`은 placeholder/default row에서만 사용한다.


---


## C) Mission Runtime State (정본)

Mission의 런타임 상태는 아래 세 가지로 해석한다.

- `ACTIVE`
  - `row.isActive == true`
  - `progressValue < conditionValue`
- `CLAIMABLE`
  - `progressValue >= conditionValue`
  - 아직 claim 하지 않음
- `COMPLETED`
  - claim 완료 상태

정본 규칙:
- `row.isActive == false`는 런타임 상태가 아니다. 테이블 데이터에만 존재하는 비활성 row다.
- `pending`, `granted` 같은 별도 상태 enum은 두지 않는다.
- `isCompleted == true`는 claim 완료 상태를 의미한다.
- claim 완료 mutation 직후에는 `SaveDataManager` 저장을 바로 시도한다.
- `conditionOp != NONE`인 미션에서만 MissionRuntime을 만든다.
- runtime 존재 = 현재 scope에서 구독/진행이 필요한 미션이 시작된 상태다.
- concrete runtime은 자신의 `conditionType` trigger를 직접 구독한다.


---


## D) MissionTriggerSystem 규약(정본)

MissionManager는 `MissionTriggerSystem`을 소유하고, 각 MissionRuntime이 이를 통해 Mission 진행도 입력을 직접 받는다.

| item | type | note |
|------|------|------|
| `MissionTriggerSystem` | `MessageSystem<int, MISSION_CONDITION_TYPE>` | 미션 전용 trigger 시스템 |
| `ownerKey` | `int` | 각 MissionRuntime의 `missionUid`; 구독 키로 그대로 사용 |
| `msgType` | `MISSION_CONDITION_TYPE` | 미션 row의 `conditionType`과 비교하는 메시지 타입 |
| `args[0]` | `CBigInt` or `long` | `msgValue`; runtime 누적용 값 |

정본 규칙:
- MissionManager는 `CompoSingleton<MissionManager>`다.
- `mTriggerSystem`은 field initializer에서 즉시 생성한다.
- daily MissionRuntime은 `ACTIVE` 상태에서만 자신의 `conditionType`으로 `MissionTriggerSystem.Subcribe(ownerKey, msgType, handler)`를 호출한다.
- achievement MissionRuntime은 runtime이 존재하는 동안 자신의 `conditionType`을 계속 구독할 수 있다.
- daily MissionRuntime은 `CLAIMABLE` / `COMPLETED` / dispose 시 자신의 ownerKey를 `UnSubcribe`한다.
- achievement MissionRuntime은 삭제/파기 시 자신의 ownerKey를 `UnSubcribe`한다.
- 미션 trigger 발행자는 `MissionManager.triggerSystem.Notify(msgType, msgValue)` 형태를 사용한다.
- payload 타입이 잘못되었거나 인자가 부족하면 해당 메시지는 무시한다.
- `MessageSystem`은 중복 구독을 허용한다. 중복 초기화를 막는 책임은 사용 측에 있다.
- `MissionTriggerSystem`은 trigger 큐/재생/영속성을 제공하지 않는다.
- MissionTriggerSystem은 trigger 전달만 담당한다. runtime 생성/복구/파기 책임은 MissionScheduler, 누적/완료/구독해지 책임은 MissionRuntime이 담당한다.

갱신/읽기 규칙:
- `MISSION_OP_TYPE.MAX` (`conditionOp=MAX`):
  - `runtime.progressValue = max(runtime.progressValue, msgValue)`
- `MISSION_OP_TYPE.SUM` (`conditionOp=SUM`):
  - 누적형 progress다.
  - `MissionRuntimeDaily`: `runtime.progressValue = min(conditionValue, runtime.progressValue + msgValue)`
  - `MissionRuntimeAchieve`: `runtime.progressValue = runtime.progressValue + msgValue`
- `MISSION_OP_TYPE.NONE` (`conditionOp=NONE`):
  - MissionRuntime을 생성하지 않는다
- claim 가능 판정:
  - `runtime.progressValue >= conditionValue && runtime.isCompleted == false`이면 `CLAIMABLE`

주의:
- `SUM`은 delta trigger 전제를 가진다. 동일 gameplay 이벤트를 중복 notify하면 값이 중복 누적된다.
- `MAX`는 "지금까지 관찰한 최고값"을 유지할 때만 사용한다.
- daily runtime의 `SUM`은 `progressValue`가 `conditionValue` 값을 넘어설 수 없다.
- achieve runtime의 `SUM`은 `progressValue`가 `conditionValue`를 넘어설 수 있다.
- daily는 init 시점과 daily reset 시점마다 `MISSION_DAY` 전체 active row를 다시 스캔하고, 그중 최대 5개만 runtime으로 생성한다.
- daily active candidate가 5개 미만이면 가능한 개수만 생성하고 나머지는 skip 한다.
- achievement는 `ClaimAsync()`에서 보상을 지급한다.
- achievement는 init 시 `MISSION_ACHIEVE` 전체 active row를 검색하고, 각 `missionId` group에 대해 runtime을 create/restore 한다.
- achievement `ClaimAsync()` 시 현재 활성 runtime 기준으로 다음 level row가 있으면 같은 runtime이 level up 한다.
- 수동 claim 모델이므로 `CLAIMABLE` runtime은 claim 전까지 유지한다.
- 다음 level row가 없으면 현재 `COMPLETED` runtime을 그대로 유지한다.
- achievement runtime은 삭제/파기 전까지 구독을 유지한다.
- achievement restore는 저장된 `level` / `progressValue` / `isCompleted`를 그대로 복원한다.
- achievement level up mutation 순서:
  1. 현재 `progressValue`를 다음 level의 `startValue`로 잡는다
  2. 기존 `conditionType` 구독을 해지한다
  3. `level`을 다음 level로 갱신한다
  4. `startValue`를 갱신한다
  5. `progressValue`는 유지한다
  6. `isCompleted = false`로 되돌린다
  7. 다음 row 기준 `conditionType`, `conditionOp`, `conditionValue` 바인딩을 교체한다
  8. 새 `conditionType`으로 다시 구독한다
- achievement level up 시 `missionUid`는 유지한다.


---


## D-2) MissionMessageSystem 규약(정본)

MissionManager는 `MissionMessageSystem`을 소유하고, mission 변화가 발생할 때마다 외부 UI/GameObject로 notify 한다.

| item | type | note |
|------|------|------|
| `MissionMessageSystem` | `MessageSystem<EntityId, MISSION_MESSAGE>` | 미션 변화 알림 시스템 |
| `ownerKey` | `EntityId` | 외부 UI GameObject의 EntityId |
| `msgType` | `MISSION_MESSAGE` | 알림 종류 |
| `args[0]` | `MissionRuntimeBase` | 해당 mission runtime (`DAY_RESET`은 예외로 no args) |
| `args[1+]` | `object` | message별 추가 payload |

정본 규칙:
- MissionManager는 `mMessageSystem`을 field initializer에서 즉시 생성한다.
- MissionManager가 notify 발행 책임을 가진다.
- MissionRuntime / MissionScheduler는 UI와 직접 결합하지 않는다.
- 외부 UI/GameObject는 자신의 `EntityId`로 subscribe/unsubscribe 한다.
- MissionMessageSystem 주석에는 callback 형식을 아래처럼 명시한다:
  - 기본: `args[0] = MissionRuntimeBase runtime`
  - 기본: `args[1+] = message-specific extra payload`
  - 예외: `DAY_RESET = no args`
- 최소 notify 시점:
  - runtime 신규 생성/복원 직후: `RUNTIME_INIT`
  - progress 변경 직후(level up 제외): `RUNTIME_PROGRESS`
  - claimable 상태 알림: `RUNTIME_CLAIMABLE`
  - claim 성공 후 reward apply 직후, save 직전: `RUNTIME_REWARDED`
  - daily reset 직후 global 1회: `DAY_RESET`
  - achievement level up 직후: `ACHIEVE_LEVEL_UP`
- 메시지별 추가 payload:
  - `RUNTIME_INIT`: 없음
  - `RUNTIME_PROGRESS`: 없음
  - `RUNTIME_CLAIMABLE`: 없음
  - `RUNTIME_REWARDED`: `args[1] = RewardData[] rewards`
  - `DAY_RESET`: no args
  - `ACHIEVE_LEVEL_UP`: 없음


---


## E) MissionClockSnapshot / MissionStorage Daily Clock 규약(정본)

MissionManager는 backend에서 아래 payload를 받아 timed mission의 현재 period를 해석한다.

| field | type | note |
|------|------|------|
| `serverNowUtcMs` | long | 서버 현재 시각 |
정본 규칙:
- `dailyMissionStartUtcMs`는 `MissionManager.Storage`에 저장한다.
- `dailyMissionStartUtcMs`는 첫 로그인(= 첫 앱 시작 동기화) 성공 시의 `serverNowUtcMs`를 저장한 값이다.
- achievement는 기간 reset이 없으므로 `periodKey = once`를 사용한다.
- 클라이언트는 `receivedAtClientUtcMs`를 로컬에 저장하고,
  `estimatedServerNow = serverNowUtcMs + (clientNowUtcMs - receivedAtClientUtcMs)`
  방식으로 현재 서버 시각을 계속 추정한다.
- timed mission period index는 클라이언트가 아래 식으로 계산한다:
  - `dailyPeriodIndex = floor(max(0, estimatedServerNowUtcMs - dailyMissionStartUtcMs) / 86400000)`
- timed mission period key는 아래 문자열 규칙을 사용한다:
  - `dailyKey = "day:{dailyPeriodIndex}"`
- `MissionManager.GetRemainTime(MISSION_TYPE.DAY)`는 현재 anchor 기준으로 다음 daily reset까지 남은 `TimeSpan`을 반환한다.
- `MissionManager.GetRemainTime(MISSION_TYPE.ACHIEVE)`는 `default(TimeSpan)`을 반환한다.
- `MissionManager.RefreshRuntimes()`는 일반적으로 현재 runtime 전체에 대해 `RUNTIME_INIT`를 재발행한다.
- 이 API의 주목적은 외부 UI의 mission 목록 재초기화다.
- 다만 `MISSION_TYPE.DAY`가 만료된 상태라면, 같은 호출에서 daily runtime reset/delete/recreate까지 수행한다.
- 이 side effect는 의도된 동작이며, 별도 API로 분리하지 않는다.
- 단, `DAY`의 남은 시간이 `TimeSpan.Zero`가 되었거나 runtime이 stale period에 속하면 daily runtime set을 reset/delete 후 현재 구간 기준으로 다시 생성한다.
- reset/recreate가 발생한 경우 `RUNTIME_INIT`는 rebuild 경로에서 한 번만 발행해야 한다.
- stale clock을 별도 예외 상태로 두지 않는다. 마지막으로 동기화한 서버 시간을 기준으로 클라이언트가 계속 판정한다.
- `getMissionClock` 호출 책임은 MissionManager에 있다.
- 로그인 시점(= 앱 시작)에는 `MissionManager.InitializeAsync()` 내부에서 `getMissionClock`을 호출한다.
- `BaseApplication.OnEnterForeground()` 같은 resume hook은 MissionManager의 `RefreshClockAsync()` 진입점만 호출한다.
- 첫 실행에서 `dailyMissionStartUtcMs`가 없으면, 첫 successful `getMissionClock` 직후 이를 기록하고 미션을 초기화한다.
- 현재 sync 시각과 `dailyMissionStartUtcMs`의 차이가 7일(`604800000ms`) 초과면 현재 `serverNowUtcMs`를 새 `dailyMissionStartUtcMs`로 사용한다.
- 첫 login에서 `getMissionClock` 호출이 실패하면 MissionManager 초기화 실패로 처리한다.


---


## F) grantId / missionUid 규칙(정본)

MissionManager/ MissionScheduler는 아래 규칙으로 `grantId`와 `missionUid`를 생성한다.

- `grantId`는 반드시 mission kind를 포함한다.
- `missionUid`는 MissionScheduler가 발급하는 증가형 `int`다.
- `missionUid`는 1부터 시작한다.
- MissionScheduler는 `nextMissionUid++`를 기본으로 사용하되, 현재 `runtimes`에 존재하는 사용 중 UID를 피해서 새 UID를 발급한다.
- period key는 `dailyMissionStartUtcMs + MissionClockSnapshot` 기준으로 계산한다.
- daily 경계는 계정별 `dailyMissionStartUtcMs` anchor에서 24시간 간격으로 끊는다.

- daily (`MISSION_DAY`):
  - `grantId = "mission:daily:{missionId}:{dailyKey}"`
- achievement (`MISSION_ACHIEVE`):
  - `grantId = "mission:achievement:{missionId}:{level}:once"`

정본 규칙:
- 새 MissionRuntime을 만들 때마다 MissionScheduler는 새 `missionUid(int)`를 발급한다.
- restore 시에는 저장된 `missionUid`를 그대로 사용한다.
- MissionRuntime은 자신의 `missionType + missionId + periodKey` 정보를 함께 저장해야 한다.
- daily runtime은 `missionId`당 1개만 존재해야 한다.
- achievement runtime은 `missionId`당 1개만 존재해야 한다.
- achievement MissionRuntime은 `level`과 `startValue`도 함께 저장해야 한다.
- daily period 전환 시 새 runtime을 만들지 않는다. 기존 daily runtime의 `periodKey`, `progressValue`, `isCompleted`를 현재 구간 기준으로 제자리 갱신한다.

연관: [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)


---


---


## H) dailyMissionStartUtcMs Reset Rule (정본)

`dailyMissionStartUtcMs`는 영구 고정값이 아니라, 기준 시각이 7일을 넘기면 다시 잡을 수 있다.

정본 규칙:
- 저장된 Mission 정보가 전혀 없으면, 첫 successful `getMissionClock.serverNowUtcMs`를
  `dailyMissionStartUtcMs`와 기준 시간으로 사용한다.
- 이후 로그인(= 앱 시작) 시점에 `getMissionClock.serverNowUtcMs`와 기존 `dailyMissionStartUtcMs`의 차이가 7일(`604800000ms`)을 초과하면,
  현재 `serverNowUtcMs`를 새로운 `dailyMissionStartUtcMs`로 사용한다.
- `dailyMissionStartUtcMs`를 새로 잡는 경우 daily 미션은 새 기준으로 다시 시작한다.
- 이 reset 시 achievement claim/progress는 유지하고, daily의 `runtimes`만 정리한다.


---


## I) Backend Callable Contract (권장 정본)

Firebase Functions 기준 mission backend callable 이름은 아래를 권장한다.

### 1) `getMissionClock`

- 목적:
  - 현재 서버 시각 제공
- 요청:
  - 빈 payload 허용
- 응답:
  - `serverNowUtcMs: long`

정본 규칙:
- Firebase는 mission 정보를 저장하지 않는다.
- backend는 미션의 조건/진행도/클레임 기록을 관리하지 않는다.
- backend의 역할은 서버 시계 제공뿐이다.


---


## J) Guest / Offline Policy (정본)

- Guest/Anonymous도 Firebase UID가 살아 있는 동안에는 backend callable을 사용할 수 있다.
- 다만 UID 지속성이 보장되지 않으므로:
  - daily는 guest 허용 가능
  - 고가치 보상은 linked account 요구 정책이 더 안전하다
- offline 상태에서는:
  - timed mission progress 누적은 로컬에서 계속 가능하다
  - timed mission claim과 period 전환 판정도 마지막으로 동기화한 서버 시간 기준으로 계속 수행한다
  - local apply 후 local/cloud save를 시도한다
  - Firebase에 mission claim을 기록하지 않으므로, 중복 방지는 local/cloud save 복원에 의존한다
  - 단, 첫 login에서 `getMissionClock`을 못 받은 상태라면 mission 초기화 자체를 하지 않는다
