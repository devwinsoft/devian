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
└─ MissionRuntimeWeekly
```

공통 필드:

- `missionId`, `missionUid`
- `periodKey`, `index`
- `progressValue`
- `state` (`MissionRuntimeState`: WAIT / ACTIVE / COMPLETED)

참고: `conditionMsgId`는 runtime 필드가 아니다. `Bind()` 시 테이블에서 조회하여 바인딩한다.

---

## Progress Source of Truth

- `DAILY`:
  - source of truth = runtime `progressValue`
  - trigger delta로 직접 누적
- `WEEKLY`:
  - source of truth = runtime `progressValue`
  - WAIT 상태에서는 누적하지 않음

---

## Trigger Rules

- 구독 키: `missionUid`
- 메시지 키: `GAME_MESSAGE_TYPE`
- `DAILY/WEEKLY`:
  - `SESSION_MAX`: `max(progressValue, delta)`
  - `SESSION_SUM`: `progressValue + delta`
  - `SESSION_MIN`: `min(progressValue, delta)`
  - `TOTAL_*`: external progress reader 기반 refresh
- WAIT runtime은 trigger를 처리하지 않는다.

---

## State Rules

- `state` 필드는 `WAIT`, `ACTIVE`, `COMPLETED` 중 하나다.
- `GetState()`는 `state == ACTIVE && 진행도 충족` 시 `CLAIMABLE`을 반환한다(파생 상태).
- `MarkCompleted()`: `state = COMPLETED` + 구독 해지.
- `TryActivate()`: `state == WAIT` 일 때만 `state = ACTIVE`로 전환.

`WEEKLY` 전용:
- 초기 생성 상태는 WAIT
- day 조건 충족 시 ACTIVE 전환 후 trigger 처리 시작

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
