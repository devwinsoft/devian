---
name: mission-scheduler
description: Use this skill when defining or implementing MissionScheduler for the mission system, especially when MissionRuntime lifetime, create/restore/rebind, daily period clear, prune, runtime lookup, or missionUid allocation must be standardized.
---

# 15-mission-scheduler

Status: ACTIVE
AppliesTo: v10
Type: Design / Scheduler SSOT


## Purpose

`MissionScheduler`는 mission runtime의 lifetime을 관리하는 내부 collaborator다.

- `MissionManager` 내부에서만 사용한다.
- singleton이 아니다.
- runtime create / restore / rebind / prune / daily clear / lookup / `missionUid` 발급을 담당한다.
- claim / save / clock refresh 같은 public orchestration은 담당하지 않는다.


---


## Responsibility Boundary

`MissionManager`
- public API
- `InitializeAsync` / `RefreshClockAsync` / `GetMissionRuntimeState` / `ClaimAsync`
- reward apply
- claim record mutation
- save 호출

`MissionScheduler`
- runtime lifetime 관리
- current scope 판단
- daily runtime 보장
- achievement runtime 보장
- restore / rebind / detach
- expired daily prune
- `missionUid` 발급
- runtime lookup

`MissionRuntimeFactory`
- concrete runtime 인스턴스 생성/복구만 담당

`MissionRuntimeBase`
- trigger 직접 구독/해지
- progress 갱신
- claimable / completed 상태 전이


---


## Hard Rules

- `MissionScheduler`는 `MissionManager`가 field로 소유하는 내부 객체다.
- `MissionScheduler`는 별도 `MonoBehaviour`나 `CompoSingleton`로 만들지 않는다.
- `MissionScheduler`는 `MissionStorage`를 새로 소유하지 않는다. 항상 `MissionManager.Storage`를 참조한다.
- `MissionScheduler`는 reward 지급을 하지 않는다.
- `MissionScheduler`는 claim record를 직접 생성하지 않는다.
- `MissionScheduler`는 `nextMissionUid++`를 기본으로 사용하되, 현재 `runtimes`에 이미 존재하는 UID는 건너뛰고 다음 빈 UID를 발급한다.
- runtime 생성/복구는 항상 `MissionRuntimeFactory`를 통해서만 수행한다.
- `conditionOp == NONE`인 row는 scheduler가 runtime을 만들지 않는다.


---


## Recommended Interface

```csharp
public sealed class MissionScheduler
{
    public MissionScheduler(
        MissionStorage storage,
        MissionTriggerSystem triggerSystem,
        Action<MissionRuntimeBase> onChanged,
        Action<MissionRuntimeBase> onClaimable);

    public void RebuildBindings(long currentServerNowUtcMs);
    public void ClearDailyScope();
    public void PruneExpiredState(long currentServerNowUtcMs);

    public MissionRuntimeDaily FindDaily(string missionId);
    public MissionRuntimeAchieve FindAchieve(string missionId);

    public int AllocateMissionUid();
}
```

정본 규칙:
- `RebuildBindings(...)`는 detach 후 current scope runtime을 다시 보장한다.
- `ClearDailyScope()`는 daily runtime reset/claim record 정리를 담당한다.
- `PruneExpiredState(...)`는 오래된 daily runtime/claim record 정리를 담당한다.
- `FindDaily(...)`, `FindAchieve(...)`는 scheduler의 공식 lookup 진입점이다.
- `AllocateMissionUid()`는 scheduler만 호출한다.


---


## Lifecycle Rules

### Initialize / Refresh

- `MissionManager.InitializeAsync()`는 clock sync와 anchor 보정 후 `MissionScheduler.RebuildBindings(...)`를 호출한다.
- `MissionManager.RefreshClockAsync()` 성공 후에도 `MissionScheduler.RebuildBindings(...)`와 `PruneExpiredState(...)`를 호출할 수 있다.

### Daily

- daily는 init/reset cycle마다 `MISSION_DAY` 전체 active row를 스캔한다.
- scheduler는 그중 최대 5개만 선택해 runtime set을 만든다.
- `fixed=true` row는 항상 먼저 포함한다.
- 남은 슬롯은 `fixed=false` active row에서 random하게 선택한다.
- 저장 runtime이 있으면 restore 하고, 없으면 create 한다.
- cycle 전환 시 기존 daily runtime set을 정리하고 새 set을 다시 만든다.
- daily는 `CLAIMABLE` / `COMPLETED` runtime을 다시 구독하지 않는다.
- period anchor를 재설정할 때 scheduler가 daily scope를 정리한다.

### Achievement

- 저장 runtime이 있으면 그 level/progress/isCompleted를 그대로 restore 한다.
- 저장 runtime이 없으면 active group마다 `level=1` row로 create 한다.
- achievement level up은 새 runtime 생성이 아니라 기존 runtime mutation이다.
- 따라서 scheduler는 claim 이후 next level create를 담당하지 않는다.


---


## Lookup Rules

- daily lookup key는 `missionId`다.
- achievement lookup key는 현재 v1에서 `missionId` 그룹당 활성 runtime 1개를 전제로 한다.
- scheduler는 lookup 실패를 null/none으로 반환하고, public error policy는 `MissionManager`가 결정한다.


---


## Why Separate Scheduler

- `MissionManager`에 runtime lifetime 로직이 과도하게 몰리면 public orchestration과 내부 state mutation이 섞인다.
- scheduler를 분리하면 claim/save/clock과 runtime create/restore/prune 경계를 분명히 나눌 수 있다.
- daily/achievement lifetime 규칙 차이도 manager가 아니라 scheduler 레벨에서 정리된다.


---


## Related

- [00-overview](../00-overview/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
