# 13-mission-runtime

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Runtime SSOT

## Purpose

`MissionRuntimeBase` 계열 runtime의 진행도/구독/상태 전이 규칙 정본이다.

---

## Runtime Model

```csharp
MissionRuntimeBase
└─ MissionRuntimeDaily
```

공통 필드:

- `missionId`, `messageId`, `missionUid`
- `periodKey`
- `progressValue`
- `isCompleted`

---

## Progress Source of Truth

- `DAY`:
  - source of truth = runtime `progressValue`
  - trigger delta로 직접 누적

---

## Trigger Rules

- 구독 키: `missionUid`
- 메시지 키: `GAME_MESSAGE_TYPE`
- `DAY`:
  - `MAX`: `max(progressValue, delta)`
  - `SUM`: `min(conditionValue, progressValue + delta)`
  - claimable/completed 시 구독 해지

---

## State Rules

- `CLAIMABLE`: `!isCompleted && progressValue >= conditionValue`
- `COMPLETED`: `isCompleted == true`
- `MarkCompleted()`는 구독 해지 동작을 포함한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-meta-message-system/11-game-message-trigger](../../45-meta-message-system/11-game-message-trigger/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
