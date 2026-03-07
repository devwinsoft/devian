---
name: mission-factory
description: Use this skill when defining or implementing MissionRuntimeFactory for mission create/restore paths with messageId binding.
---

# 14-mission-factory

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Factory SSOT

## Purpose

`MissionRuntimeFactory`는 runtime create/restore 경로를 표준화한다.

- `missionUid` 발급/저장소 조회는 담당하지 않는다.
- `messageId` + `MISSION_STAT(statType/opType)` 바인딩을 생성한다.

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
    public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args);
}
```

---

## Args (정본)

`DailyMissionRuntimeCreateArgs`

- `MissionId`, `MessageId`
- `PeriodKey`, `MissionUid`, `Index`
- `StatType`, `OpType`, `ConditionValue`
- `SubscribeTrigger`, `UnsubscribeTrigger`, callbacks

`MissionRuntimeRestoreArgs`

- create args + `ProgressValue`, `IsCompleted`

---

## Create/Restore Rules

- Daily create:
  - `progressValue = 0`
  - internal progress mode
- Restore:
  - 저장된 progress 사용

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [15-mission-scheduler](../15-mission-scheduler/SKILL.md)
