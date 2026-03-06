---
name: mission-factory
description: Use this skill when defining or implementing MissionRuntimeFactory for mission create/restore paths with missionStatId binding and achieve external-progress reader wiring.
---

# 14-mission-factory

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Factory SSOT

## Purpose

`MissionRuntimeFactory`는 runtime create/restore 경로를 표준화한다.

- `missionUid` 발급/저장소 조회는 담당하지 않는다.
- `missionStatId` + `MISSION_STAT(statType/opType)` 바인딩을 생성한다.
- achieve external progress reader 연결을 담당한다.

---

## Hard Rules

- Factory는 `missionUid`를 생성하지 않는다.
- create/restore 분기를 분리한다.
- `opType == NONE` row는 scheduler 단계에서 걸러진다(Factory에 전달 금지).
- restore는 타입 fallback을 허용하지 않는다.

---

## Factory Shape

```csharp
public static class MissionRuntimeFactory
{
    public static MissionRuntimeDaily CreateDaily(DailyMissionRuntimeCreateArgs args);
    public static MissionRuntimeAchieve CreateAchieve(AchieveMissionRuntimeCreateArgs args);
    public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args);
}
```

---

## Args (정본)

`DailyMissionRuntimeCreateArgs`

- `MissionType`, `MissionId`, `MissionStatId`
- `PeriodKey`, `MissionUid`, `Index`
- `StatType`, `OpType`, `ConditionValue`
- `TriggerSystem`, callbacks

`AchieveMissionRuntimeCreateArgs`

- `MissionType`, `MissionId`, `MissionStatId`
- `Level`, `PeriodKey`, `MissionUid`
- `StatType`, `OpType`, `ConditionValue`
- `ReadProgress` (`Func<CBigInt>`)
- `TriggerSystem`, callbacks

`MissionRuntimeRestoreArgs`

- 공통: create args + `ProgressValue`, `IsCompleted`
- achieve restore는 `ReadProgress`를 반드시 전달한다.

---

## Create/Restore Rules

- Daily create:
  - `progressValue = 0`
  - internal progress mode
- Achieve create:
  - `progressValue = 0`
  - external progress mode(`ReadProgress`)
- Restore:
  - daily는 저장 progress 사용
  - achieve는 external reader로 sync되는 구조를 사용

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [15-mission-scheduler](../15-mission-scheduler/SKILL.md)
