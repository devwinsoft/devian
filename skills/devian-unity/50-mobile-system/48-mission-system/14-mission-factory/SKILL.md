---
name: mission-factory
description: Use this skill when defining or implementing MissionRuntimeFactory for mission create/restore paths with message binding.
---

# 14-mission-factory

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Factory SSOT

## Purpose

`MissionRuntimeFactory`는 runtime create/restore 경로를 표준화한다.

- `missionUid` 발급/저장소 조회는 담당하지 않는다.
- `conditionMsgId + GAME_MESSAGE(messageType/saveType/conditionOp)` 바인딩을 생성한다.

---

## Hard Rules

- Factory는 `missionUid`를 생성하지 않는다.
- create/restore 분기를 분리한다.
- `saveType == NONE` row는 scheduler 단계에서 걸러진다(Factory에 전달 금지).
- restore는 타입 fallback을 허용하지 않는다.

---

## Factory Shape

```csharp
public static class MissionRuntimeFactory
{
    public static MissionRuntimeDaily CreateDaily(DailyMissionRuntimeCreateArgs args);
    public static MissionRuntimePeriod CreatePeriod(PeriodMissionRuntimeCreateArgs args);
    public static MissionRuntimeBase Restore(MissionRuntimeRestoreArgs args);
}
```

---

## Args (정본)

`DailyMissionRuntimeCreateArgs`

- `MissionId`, `PeriodKey`, `MissionUid`, `Index`
- `StatType`, `OpType`, `ConditionOpType`, `ConditionValue`
- `SubscribeTrigger`, `UnsubscribeTrigger`, `ReadExternalProgress`, callbacks

`PeriodMissionRuntimeCreateArgs`

- daily args + `Day`, `IsWaiting`

`MissionRuntimeRestoreArgs`

- `MissionType` + create args + `ProgressValue`, `State`

참고:
- `ConditionMsgId`/`MessageId`는 Args에 포함하지 않는다. scheduler가 테이블에서 조회하여 `Bind()` 파라미터(`StatType`, `OpType` 등)로 전달한다.
- RestoreArgs의 `State`는 `MissionRuntimeState` enum이다(WAIT/ACTIVE/COMPLETED).

---

## Create/Restore Rules

- Daily create:
  - `progressValue = 0`
  - 기본 ACTIVE
- Period create:
  - `progressValue = 0`
  - 기본 WAIT (`day == 1` row만 scheduler에서 즉시 ACTIVE 전환 가능)
- Restore:
  - 저장된 progress/state를 그대로 사용

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [15-mission-scheduler](../15-mission-scheduler/SKILL.md)
