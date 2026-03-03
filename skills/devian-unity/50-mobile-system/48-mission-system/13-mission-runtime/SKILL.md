# 13-mission-runtime


Status: ACTIVE
AppliesTo: v10
Type: Design / Runtime SSOT


## Purpose

`MissionRuntimeBase`는 MissionManager가 `MissionScheduler`를 통해 소유하는 **미션 1개당 runtime 상태 객체의 추상 베이스**다.

- key는 `missionUid`다.
- `MissionRuntimeDaily` / `MissionRuntimeAchieve`가 실제 concrete runtime이다.
- runtime 생성 = 해당 scope에서 미션 시작이다.
- started / completed / reset 개념은 runtime의 용어다.
- `conditionOp`는 runtime의 `ProgressValue`를 어떤 방식으로 갱신할지 결정한다.


---


## Ownership

- `MissionManager`는 `CompoSingleton<MissionManager>`다.
- `MissionManager`가 `MissionRuntimeBase` 계열 runtime을 단일 소유한다.
- `MissionStorage.runtimes[missionUid]`가 runtime 저장 정본이다.
- `MissionTriggerSystem`은 입력 라우터다.
- MissionScheduler는 runtime 생성/복구/reset/파기를 담당하고, MissionManager는 저장 orchestration을 담당한다.
- `MissionRuntimeBase`는 공통 상태/공통 흐름을 담당한다.
- concrete runtime은 trigger 구독/해지, 누적, claim 가능 판정의 차이를 override로 구현한다.


---


## Core Terms

- `mission start`
  - 해당 미션 definition의 runtime을 처음 생성하는 시점
- `mission claimable`
  - 현재 `ProgressValue`가 `conditionValue` 이상이고 아직 claim 하지 않은 상태
- `mission completed`
  - claim 완료 상태
- `reset`
  - 기존 runtime을 현재 구간 기준으로 제자리 초기화하는 방식
- `ProgressValue`
  - 현재 runtime이 저장하는 단일 누적값


---


## Runtime Data Model

권장 runtime 구조:

```csharp
public abstract class MissionRuntimeBase
{
    public MISSION_TYPE missionType;
    public string missionId = "";
    public string periodKey = "";
    public int missionUid;
    public CBigInt progressValue;
    public bool isCompleted;
    public abstract int Index { get; }

    protected abstract void SubscribeCore(MissionTriggerSystem triggerSystem);
    protected abstract void UnsubscribeCore(MissionTriggerSystem triggerSystem);
    protected abstract void OnClaimableCore();
    protected abstract void OnCompletedCore();
}

public sealed class MissionRuntimeDaily : MissionRuntimeBase
{
}

public sealed class MissionRuntimeAchieve : MissionRuntimeBase
{
    public int level;
    public CBigInt startValue;
}
```

정본 규칙:
- `MissionRuntimeBase`는 `missionUid` 단위 객체다.
- concrete runtime은 `MissionRuntimeDaily`, `MissionRuntimeAchieve` 두 종류다.
- runtime 존재 = 해당 미션 definition에서 미션이 시작되었음을 의미한다.
- 별도 `isStarted` bool은 두지 않는다.
- 저장 필드 이름은 `ProgressValue`를 사용한다.
- 저장/표시 정렬용 runtime 프로퍼티 이름은 `Index`를 사용한다.
- `Index`는 0-based다.
- 계산용 읽기 프로퍼티가 따로 필요하면 `CurrentProgress` 같은 이름을 별도로 둘 수 있다.
- `isCompleted == true`는 claim 완료 상태를 의미한다.
- daily/achievement 차이는 abstract base의 override로 구현한다. interface는 두지 않는다.


---


## Creation Rules

runtime 생성 규칙:

1. 현재 scope의 mission row가 `ACTIVE`이고 `conditionOp != NONE`이면 현재 `periodKey`를 계산한다.
2. 현재 미션 definition과 일치하는 저장 runtime이 없으면 새 concrete runtime을 생성한다.
   - daily: 현재 cycle에서 선택된 row에 대해서만 create
   - achievement: active group별 현재 row에 대해 create/restore
3. 새 생성(create) 시 기본값:
   - daily: `progressValue = 0`
   - achievement: `progressValue = startValue`
   - `isCompleted = false`
4. 저장된 runtime이 있으면 restore 한다:
   - `progressValue`는 저장값을 그대로 사용한다.
   - `isCompleted`는 저장값을 그대로 사용한다.
5. create 시 `missionUid`는 `MissionScheduler`가 `MissionStorage.nextMissionUid++`를 기본으로 사용하되, 현재 `runtimes`에 이미 존재하는 UID는 건너뛰고 다음 빈 `int` UID를 발급한다.
6. create / restore 모두 row의 `conditionType`, `conditionOp`, `conditionValue`와 `MissionTriggerSystem`을 받아 concrete runtime을 재구성한다.
7. achievement create는 `missionId + level + startValue`를 기준 정보로 사용한다.

정본 규칙:
- 앱 시작 시 `InitializeAsync()`는 daily는 최대 5개 selected row, achievement는 active group 전체에 대해 runtime을 보장해야 한다.
- 새 period 진입 시 daily runtime set을 다시 만든다.
- daily selected row는 `fixed=true` 우선, 나머지는 random selection으로 결정한다.
- daily `CLAIMABLE` 또는 `COMPLETED` 상태로 restore 된 runtime은 다시 구독하지 않는다.
- achievement runtime은 삭제/파기 전까지 구독을 유지한다.
- `conditionOp == NONE`인 row는 runtime을 생성하지 않는다.
- restore는 저장된 MissionRuntime을 앱 시작/load 시 재구성할 때만 사용한다.
- daily는 현재 cycle에서 선택된 row에 대해서만 runtime이 존재해야 한다.
- achievement는 group별 현재 runtime 1개만 존재해야 한다.
- achievement level up은 새 runtime 생성이 아니라 같은 runtime mutation이다.
- daily runtime은 `level = 1`, `startValue = 0`을 사용한다.
- achievement 최초 create는 `level = 1` row에서 시작한다.
- daily runtime의 `Index`는 최종 선택된 row를 `orderNum ASC`, `missionId ASC`로 정렬한 뒤 0부터 다시 부여한다.
- achievement runtime의 `Index`는 현재 바인딩 row의 `orderNum - 1`을 반환한다.


---


## Recording Rules

runtime은 자신이 생성될 때 등록한 `conditionType` trigger를 직접 받는다.

1. payload 타입이 맞지 않으면 무시한다.
2. daily는 claimable/completed면 더 이상 구독하지 않는다. achievement는 삭제/파기 전까지 계속 구독한다.
3. 자신의 `conditionOp`를 읽는다.
4. 아래 규칙으로 `progressValue`를 갱신한다.

- `MISSION_OP_TYPE.MAX`
  - `runtime.progressValue = max(runtime.progressValue, value)`
- `MISSION_OP_TYPE.SUM`
  - 누적형 progress다.
  - `MissionRuntimeDaily`: `runtime.progressValue = min(conditionValue, runtime.progressValue + value)`
  - `MissionRuntimeAchieve`: `runtime.progressValue = runtime.progressValue + value`

정본 규칙:
- 갱신 규칙은 각 mission row의 `conditionOp`가 직접 결정한다.
- trigger payload 타입은 `CBigInt` 기준 확장을 권장한다. 편의용 `long` overload는 허용한다.
- daily는 `CLAIMABLE` 또는 `COMPLETED` 상태에서 더 이상 누적하지 않는다.
- achievement는 삭제/파기 전까지 누적을 계속한다.


---


## Read Rules

Mission row는 자신의 runtime `progressValue`를 읽어 완료를 판정한다.

- `MISSION_OP_TYPE.MAX`
  - `progressValue`는 max 갱신 결과다
- `MISSION_OP_TYPE.SUM`
  - daily runtime에서는 `conditionValue`를 넘지 않는다
  - achieve runtime에서는 `conditionValue`를 넘어설 수 있다
- `MISSION_OP_TYPE.NONE`
  - `progressValue`를 갱신하지 않는다. placeholder/default 전용이다.

claim 가능 판정:
- `progressValue >= conditionValue && runtime.isCompleted == false`이면 `CLAIMABLE`
- `CLAIMABLE` 시 MissionManager callback을 호출한다.
- daily는 `CLAIMABLE` 시 자신의 구독을 해지할 수 있다.


---


## Complete / Reset Rules

- runtime claimable
  - `progressValue >= conditionValue`
  - `runtime.isCompleted == false`
  - claimable 자체가 `progressValue`를 reset하지 않는다.
  - daily는 claimable 시 runtime이 자신의 구독을 해지할 수 있다.

- runtime completed
  - claim 성공 후 `runtime.isCompleted = true`
  - completed 자체가 `progressValue`를 reset하지 않는다.
  - daily는 completed 시 runtime이 자신의 구독을 해지한다.
  - achievement final level은 completed 후에도 유지된다.

- daily reset
  - 새 `dailyKey`가 되면 기존 daily runtime set을 정리한다.
  - `MISSION_DAY` 전체 active row 중 최대 5개를 다시 선택해 새 MissionRuntimeDaily를 생성한다.
  - `fixed=true` row는 항상 포함한다.
  - 새 runtime은 새 `missionUid`, 새 `periodKey`, `progressValue = 0`, `isCompleted = false`로 시작한다.

- achievement
  - `once` scope를 사용하므로 자동 period reset이 없다.
  - `CLAIMABLE`이 되어도 다음 level을 생성하지 않는다.
  - `ClaimAsync()`에서 보상을 지급한다.
  - claim 성공 시 다음 level row가 있으면 같은 runtime이 level up 한다.
  - 수동 claim 모델이므로 현재 `CLAIMABLE` runtime은 claim 전까지 storage에 유지한다.
  - 다음 level row가 없으면 현재 `COMPLETED` runtime을 그대로 유지한다.
  - restore 시에는 저장된 `level` / `progressValue` / `isCompleted`를 그대로 사용한다.
  - level up 시에는 새 runtime을 만들지 않는다.
  - level up 순서:
    1. 현재 `progressValue`를 다음 level `startValue`로 잡는다
    2. 기존 `conditionType` 구독을 해지한다
    3. 같은 runtime의 `level` / `startValue` / `isCompleted`를 갱신한다
    4. 다음 row 기준 condition 바인딩을 교체한다
    5. `progressValue`는 유지한다
    6. 새 `conditionType`으로 다시 구독한다

정본 규칙:
- `progressValue`는 runtime 생성 시 create args의 초기값으로 시작한다.
- daily create는 `0`에서 시작하고, achievement create는 `startValue`에서 시작한다.
- daily reset은 기존 runtime set 폐기 후 새 runtime set 생성이다.
- achievement level up은 같은 `missionUid` runtime에서 일어난다.


---


## Responsibilities Boundary

`MissionTriggerSystem`
- trigger 라우팅만 담당

`MissionRuntimeBase`
- trigger 직접 구독/해지
- 미션 1개의 현재 scope 누적값(`ProgressValue`) 보유
- claim 완료 상태 보유
- claim 가능 시 MissionManager에 callback

`MissionRuntimeDaily`
- daily 전용 구독 해지 규칙을 override 한다
- `CLAIMABLE` / `COMPLETED`에서 구독을 해지한다

`MissionRuntimeAchieve`
- achievement 전용 claim/삭제 규칙을 override 한다
- 삭제 전까지 구독을 유지한다

`MissionManager`
- row 조회
- runtime 생성/저장
- runtime callback 처리
- claim 처리
- save/load orchestration


---


## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
