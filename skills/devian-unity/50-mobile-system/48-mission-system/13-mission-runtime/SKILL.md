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
└─ MissionRuntimeAchieve
```

공통 필드:

- `missionType`, `missionId`, `missionStatId`, `missionUid`
- `periodKey`
- `progressValue`
- `isCompleted`

---

## Progress Source of Truth

- `DAY`:
  - source of truth = runtime `progressValue`
  - trigger delta로 직접 누적
- `ACHIEVE`:
  - source of truth = `MissionStorage.stats[missionStatId]`
  - runtime은 external reader를 통해 값을 읽고 반영
  - trigger delta를 runtime 내부에서 직접 누적하지 않는다

---

## Trigger Rules

- 구독 키: `missionUid`
- 메시지 키: `GAME_MESSAGE_TYPE`
- `DAY`:
  - `MAX`: `max(progressValue, delta)`
  - `SUM`: `min(conditionValue, progressValue + delta)`
  - claimable/completed 시 구독 해지
- `ACHIEVE`:
  - runtime 존재 동안 구독 유지(완료 시 해지)
  - delta 처리 시 external reader 값으로 sync

---

## State Rules

- `CLAIMABLE`: `!isCompleted && progressValue >= conditionValue`
- `COMPLETED`: `isCompleted == true`
- `MarkCompleted()`는 구독 해지 동작을 포함한다.

---

## Level-Up (ACHIEVE)

다음 level row가 있으면 같은 runtime을 mutation한다.

순서:

1. 기존 구독 해지
2. `level` 갱신, `isCompleted = false`
3. 새 row 기준 `missionStatId/statType/opType/conditionValue` 교체
4. 새 `missionStatId` external reader 연결
5. external progress refresh
6. 새 statType으로 재구독

주의:

- level-up에서 progress 수동 set 금지
- level-up 후 source of truth는 새 `missionStatId`의 `stats`다

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
