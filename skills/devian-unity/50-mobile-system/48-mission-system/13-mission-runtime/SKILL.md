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
├─ MissionRuntimeDaily
└─ MissionRuntimePeriod
```

공통 필드:

- `missionId`, `conditionMsgId`, `missionUid`
- `periodKey`, `index`
- `progressValue`
- `isWaiting`, `isCompleted`

---

## Progress Source of Truth

- `DAILY`:
  - source of truth = runtime `progressValue`
  - trigger delta로 직접 누적
- `PERIOD`:
  - source of truth = runtime `progressValue`
  - WAIT 상태에서는 누적하지 않음

---

## Trigger Rules

- 구독 키: `missionUid`
- 메시지 키: `GAME_MESSAGE_TYPE`
- `DAILY/PERIOD`:
  - `SESSION_MAX`: `max(progressValue, delta)`
  - `SESSION_SUM`: `progressValue + delta`
  - `SESSION_MIN`: `min(progressValue, delta)`
  - `TOTAL_*`: external progress reader 기반 refresh
- WAIT runtime은 trigger를 처리하지 않는다.

---

## State Rules

- `WAIT`: `isWaiting == true`
- `CLAIMABLE`: `!isWaiting && !isCompleted && IsClaimable`
- `COMPLETED`: `isCompleted == true`
- `MarkCompleted()`는 구독 해지 동작을 포함한다.

`PERIOD` 전용:
- 초기 생성 상태는 WAIT
- day 조건 충족 시 ACTIVE 전환 후 trigger 처리 시작

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
