# 03-ssot — 48-mission-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `MISSION`, `MISSION_STAT` 스키마
- `GAME_MESSAGE_TYPE`, `GAME_MESSAGE_SAVE_TYPE`, `MISSION_MESSAGE` 사용 규약
- Mission trigger/update/runtime binding 규칙
- Mission 저장 구조(`MissionStorage`, daily runtime, stats) 규칙

업적(`ACHIEVE`) runtime/claim/save 정본은
[46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)에서 관리한다.

---

## A) Core Terms

- `missionId`: daily 미션 ID
- `missionType`: `MISSION_TYPE.DAY`만 MissionManager에서 처리
- `missionStatId`: mission row가 참조하는 stat key
- `statType`: trigger key (`GAME_MESSAGE_TYPE`)
- `opType`: stat 누적 연산 (`GAME_MESSAGE_SAVE_TYPE`)
- `conditionValue`: 목표값(`CBigInt`)
- `periodKey`: `day:{dailyPeriodIndex}`
- `missionUid`: runtime 식별 `int`
- `mission stats`: `MissionStorage.stats[string missionStatId]`

---

## B) Table Schema

### `MISSION`

| field | type | note |
|---|---|---|
| `missionId` | string (pk) | 미션 ID |
| `missionType` | `MISSION_TYPE` | 미션 유형 (NONE/DAY/PASS_FREE/PASS_PAID) |
| `isActive` | bool | 운영 토글 |
| `fixed` | bool | daily 선택 우선 포함 |
| `orderNum` | int | 정렬 기준(1-base) |
| `missionStatId` | string | `MISSION_STAT.missionStatId` FK |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키 |

### `MISSION_STAT`

| field | type | note |
|---|---|---|
| `missionStatId` | string (pk) | stat 식별자 |
| `statType` | `GAME_MESSAGE_TYPE` | trigger 타입 |
| `opType` | `GAME_MESSAGE_SAVE_TYPE` | 누적 방식 |

정본 source:
- `input/Domains/Game/MissionTable.xlsx`
- `input/Domains/Game/ENUM_MISSION.json`

---

## C) Runtime State

- `ACTIVE`: `!isCompleted && progressValue < conditionValue`
- `CLAIMABLE`: `!isCompleted && progressValue >= conditionValue`
- `COMPLETED`: `isCompleted == true`

규칙:
- `opType == NONE` row는 runtime을 생성하지 않는다.
- daily는 현재 cycle에서 선택된 row만 runtime을 만든다(최대 5).

---

## D) Trigger + Stats Update

`GameMessageTrigger` 정본 타입:

```csharp
BaseTrigger<int, GAME_MESSAGE_TYPE>
```

외부 진입점은 `MissionManager.Notify(statType, delta)`다.

trigger 처리 순서:

1. `MissionManager.Notify` -> `GameMessageManager.NotifyGameMessage` 위임
2. `TB_MESSAGE`에서 `statType` 일치 row를 순회
3. `message.stats[messageId]` 갱신
   - `SUM`: `current + delta`
   - `MAX`: `max(current, delta)`
4. `GameMessageTrigger` publish로 daily runtime 구독자 notify
5. `AchieveManager.Notify`로 동일 이벤트 전달

runtime 반영 (`DAY`):

- `MAX`: `max(progressValue, delta)`
- `SUM`: `min(conditionValue, progressValue + delta)`

---

## E) Storage / Save Rules

`MissionStorage` 정본 필드:

- `schemaVersion` (기본 2)
- `dailyMissionStartUtcMs`
- `clockSnapshot`
- `clockReceivedAtClientUtcMs`
- `nextMissionUid`
- `runtimes: Dictionary<int, MissionRuntimeBase>`
- `stats: Dictionary<string, CBigInt>`

runtime 저장 규칙 (`DAY`만):

- `missionId`, `missionStatId`, `missionUid`, `isCompleted`
- `periodKey`, `index`, `progressValue`

---

## F) Daily Clock Rules

- anchor: `MissionStorage.dailyMissionStartUtcMs`
- period index:
  - `floor(max(0, estimatedServerNowUtcMs - dailyMissionStartUtcMs) / 86400000)`
- daily key: `day:{index}`
- 현재 sync 시각과 anchor 차이가 7일 초과면 anchor를 현재 서버 시각으로 재설정한다.

---

## G) Grant/Uid Rules

- `missionUid`는 scheduler가 발급하는 증가형 int
- 발급 시 사용 중 UID는 건너뛴다

---

## Related

- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [01-policy](../01-policy/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
