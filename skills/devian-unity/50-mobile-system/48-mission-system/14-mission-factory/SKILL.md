---
name: mission-factory
description: Use this skill when defining or implementing MissionRuntimeFactory for the mission system, especially when daily and achievement runtime creation args differ, when restore/create paths must be separated, or when MissionRuntime subscription wiring must be standardized.
---

# 14-mission-factory

Status: ACTIVE
AppliesTo: v10
Type: Design / Factory SSOT


## Purpose

`MissionRuntimeFactory`는 `MissionRuntimeBase` 계열 runtime의 create / restore 경로를 표준화한다.

- 생성 책임만 가진다.
- `missionUid` 발급 책임은 가지지 않는다.
- 저장소 조회 책임도 가지지 않는다.
- daily / achievement 생성 args 차이를 문서로 고정한다.


---


## Responsibility Boundary

`MissionManager`
- public API 오케스트레이션
- claim / save / clock refresh

`MissionScheduler`
- row 조회
- 현재 scope 판단
- 기존 runtime 존재 여부 판단
- `missionUid` 발급
- create / restore 분기

`MissionRuntimeFactory`
- concrete runtime 인스턴스 구성
- 기본값 세팅
- restore 값 주입
- 구독 연결 여부 결정

`MissionRuntimeBase`
- trigger 직접 구독/해지
- `ProgressValue` 갱신
- claim 가능 판정


---


## Hard Rules

- Factory는 `missionUid`를 새로 만들지 않는다.
- Factory는 `MissionStorage.runtimes`를 직접 조회하지 않는다.
- create와 restore는 분리된 진입점을 가진다.
- `conditionOp == NONE`이면 Factory는 runtime을 만들지 않는다.
- runtime 구독 ownerKey는 `missionUid(int)`를 그대로 사용한다.
- `missionUid`는 MissionScheduler가 `nextMissionUid++`를 기본으로 발급하되, 사용 중 UID는 건너뛴다.
- daily restore는 `CLAIMABLE`/`COMPLETED`면 구독하지 않는다.
- achievement restore는 삭제/파기 전까지 구독을 유지한다.


---


## Factory Shape

권장 인터페이스:

```csharp
public static class MissionRuntimeFactory
{
    public static MissionRuntimeDaily CreateDaily(DailyMissionRuntimeCreateArgs args);
    public static MissionRuntimeAchieve CreateAchieve(AchieveMissionRuntimeCreateArgs args);
    public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args);
}
```

정본 규칙:
- `CreateDaily`와 `CreateAchieve`는 분리한다.
- achievement는 단계형 생성 규칙이 daily와 다르므로 공통 create args로 합치지 않는다.
- `Restore`는 저장된 `progressValue` / `isCompleted` / `missionUid`를 그대로 사용한다.
- `Restore`는 저장된 runtime type에 따라 concrete subclass를 복원한다.
- `Restore`는 저장된 runtime을 앱 시작/load 시 재구성할 때만 사용한다.
- 새 daily period 진입은 restore가 아니라 새 create 경로를 사용한다.
- achievement level up은 새 runtime 생성이 아니라 같은 runtime mutation이다.
- achievement level up에는 Factory를 사용하지 않는다.


---


## Create Args

### Daily

```csharp
public readonly struct DailyMissionRuntimeCreateArgs
{
    public MISSION_TYPE MissionKind;         // DAY
    public string MissionId;                // row id
    public string PeriodKey;                // day:{index}
    public int MissionUid;                  // manager allocated
    public MISSION_CONDITION_TYPE ConditionType;
    public MISSION_OP_TYPE ConditionOp;
    public CBigInt ConditionValue;
    public string RewardGroupId;
}
```

규칙:
- daily는 `missionId` 단일 ID를 사용한다.
- 새 runtime의 `ProgressValue`는 0에서 시작한다.
- `startValue`를 받지 않는다.
- daily create는 scheduler가 현재 cycle에서 선택한 row에 대해서만 호출한다.
- daily selection 자체는 factory 책임이 아니다.


### Achievement

```csharp
public readonly struct AchieveMissionRuntimeCreateArgs
{
    public MISSION_TYPE MissionKind;         // ACHIEVE
    public string MissionId;                // group id
    public int Level;                       // step level
    public string PeriodKey;                // once
    public int MissionUid;                  // manager allocated
    public CBigInt StartValue;              // previous completed progress
    public MISSION_CONDITION_TYPE ConditionType;
    public MISSION_OP_TYPE ConditionOp;
    public CBigInt ConditionValue;
    public string RewardGroupId;
}
```

규칙:
- achievement의 `missionId`는 그룹 ID다.
- 실제 단계 미션 식별은 `missionId + level`이다.
- achievement definition의 `missionId + level` 유일성은 data layer에서 보장한다.
- `StartValue`는 직전 완료 미션의 `ProgressValue`다.
- 최초 create 시 `StartValue`는 보통 `0`이다.
- 최초 create는 `level=1` row를 사용한다.
- level up은 새 runtime create가 아니라 같은 runtime mutation으로 처리한다.


### Restore

```csharp
public readonly struct MissionRuntimeRestoreArgs
{
    public MISSION_TYPE MissionKind;
    public string MissionId;
    public string PeriodKey;
    public int MissionUid;
    public int? Level;                      // achievement only
    public CBigInt StartValue;
    public CBigInt ProgressValue;
    public bool IsCompleted;
    public MISSION_CONDITION_TYPE ConditionType;
    public MISSION_OP_TYPE ConditionOp;
    public CBigInt ConditionValue;
    public string RewardGroupId;
}
```


---


## Create Rules

### Daily create

- `conditionOp != NONE`일 때만 생성한다.
- `ProgressValue = 0`
- `IsCompleted = false`
- 생성 직후 구독 시작
- scheduler는 `MISSION_DAY` active row 전체 중 최대 5개까지만 `CreateDaily(...)`를 호출한다.

### Achievement create

- `conditionOp != NONE`일 때만 생성한다.
- `ProgressValue = StartValue`
- `IsCompleted = false`
- 생성 직후 구독 시작
- scheduler는 init 시 `MISSION_ACHIEVE` active row 전체를 검색하고 group별로 `CreateAchieve(...)` 또는 `Restore(...)`를 결정한다.
- 현재 level이 `CLAIMABLE`이 되어도 다음 level을 생성하지 않는다.
- `ClaimAsync()` 성공 후 다음 level row가 있으면 같은 runtime이 level up 한다.
- 수동 claim 모델이므로 현재 `CLAIMABLE` runtime은 claim 전까지 storage에 유지한다.
- 다음 level row가 없으면 현재 `COMPLETED` runtime을 그대로 유지한다.
- 즉, achievement next level 전환은 `CreateAchieve(...)`가 아니라 기존 runtime mutation으로 처리한다.

### Restore

- `ProgressValue`는 저장값을 그대로 사용한다.
- `IsCompleted`는 저장값을 그대로 사용한다.
- daily는 `ACTIVE` 상태일 때만 구독한다.
- achievement는 삭제/파기 전까지 구독을 유지한다.
- 앱 시작 시 `MissionStorage.runtimes`를 메모리로 재구성할 때 사용한다.
- local/cloud save에서 저장된 MissionRuntime을 복원할 때 사용한다.
- daily restore는 `Level = 1`, `StartValue = 0`을 사용한다.
- achievement restore는 저장된 `Level` / `StartValue` / `ProgressValue` / `IsCompleted`를 그대로 사용한다.


---


## Subscription Rule

Factory는 아래 조건을 만족할 때만 runtime 구독을 연결해야 한다.

- `row.isActive == true`
- `conditionOp != NONE`

정본 규칙:
- daily는 `progressValue < conditionValue && isCompleted == false`일 때만 구독한다.
- achievement는 runtime이 존재하는 동안 구독을 유지한다.


---


## Why Daily / Achievement Args Differ

- daily는 매 cycle마다 선택된 row set만 생성하는 반복 미션이다.
- achievement는 `missionId`가 그룹 ID이고 `level`이 실제 단계다.
- achievement 최초 create와 restore에만 factory args가 필요하고, 이후 level 전환은 runtime mutation이다.
- 따라서 두 create 경로를 하나의 args로 억지 통합하지 않는다.


---


## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [15-mission-scheduler](../15-mission-scheduler/SKILL.md)
