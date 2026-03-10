# 03-ssot — 48-mission-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `MISSION_DAILY`, `MISSION_PERIOD`, `MESSAGE_META` 스키마
- `MISSION_TYPE`, `MESSAGE_META_*`, `MESSAGE_MISSION_TYPE` 사용 규약
- Mission trigger/update/runtime binding 규칙
- Mission 저장 구조(`MissionStorage`, daily/period runtime) 규칙

업적(`ACHIEVE`) runtime/claim/save 정본은
[46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)에서 관리한다.

---

## A) Core Terms

- `missionId`: 미션 ID
- `missionType`: `MISSION_TYPE.DAILY` / `MISSION_TYPE.PERIOD`
- `conditionMsgId`: mission row가 참조하는 `MESSAGE_META.messageId`
- `messageType`: trigger key (`MESSAGE_META_TYPE`)
- `saveType`: stat 누적 연산 (`MESSAGE_META_SAVE_TYPE`)
- `conditionOp`: 비교 연산 (`MESSAGE_META_OP_TYPE`)
- `conditionValue`: 목표값(`CBigInt`)
- `periodKey`: runtime 주기 식별자 (`daily:{index}`, `period:{index}`)
- `periodDayGroupKey`: `MISSION_PERIOD.day` (period runtime 활성화 그룹 키)
- `missionUid`: runtime 식별 `int`
- `message stats`: `GameMessageStorage.stats[string messageId]`

---

## B) Table Schema

### `MISSION_DAILY`

| field | type | note |
|---|---|---|
| `missionId` | string (pk) | 미션 ID |
| `isActive` | bool | 운영 토글 |
| `fixed` | bool | daily 선택 우선 포함 |
| `orderNum` | int | 정렬 기준(1-base) |
| `conditionMsgId` | string | `MESSAGE_META.messageId` FK |
| `conditionOp` | `MESSAGE_META_OP_TYPE` | 조건 비교 타입 |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키 |

규칙:
- `missionType` 필드는 없다(`MISSION_DAILY` 자체가 DAILY 타입).

### `MISSION_PERIOD`

| field | type | note |
|---|---|---|
| `missionId` | string (pk) | 미션 ID |
| `isActive` | bool | 운영 토글 |
| `day` | int | 활성화 day (1~7) |
| `conditionMsgId` | string | `MESSAGE_META.messageId` FK |
| `conditionOp` | `MESSAGE_META_OP_TYPE` | 조건 비교 타입 |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키 |

규칙:
- `missionType`, `fixed`, `orderNum` 필드는 없다.
- `day`는 `1~7` 범위를 사용한다.
- `day`는 period runtime의 group key다. 동일 `day` row는 같은 activation bucket으로 처리한다.

### `MESSAGE_META`

| field | type | note |
|---|---|---|
| `messageId` | string (pk) | message 식별자 |
| `messageType` | `MESSAGE_META_TYPE` | trigger 타입 |
| `saveType` | `MESSAGE_META_SAVE_TYPE` | 누적 방식 |

정본 source:
- `input/Domains/Game/MetaTable.xlsx`
- `input/Domains/Game/ENUM_META.json`

---

## C) Runtime State

- `WAIT`: `isWaiting == true`
- `ACTIVE`: `!isWaiting && !isCompleted && !IsClaimable`
- `CLAIMABLE`: `!isWaiting && !isCompleted && IsClaimable`
- `COMPLETED`: `isCompleted == true`

규칙:
- `saveType == NONE` row는 runtime을 생성하지 않는다.
- `PERIOD` runtime은 초기화/리셋 직후 기본 상태가 WAIT다.

---

## D) Trigger + Stats Update

`GameMessageTrigger` 정본 타입:

```csharp
BaseTrigger<int, MESSAGE_META_TYPE>
```

외부 진입점은 `MetaMessageManager.Notify(messageType, delta)`다.

trigger 처리 순서:

1. `MetaMessageManager.Notify` 호출
2. `TB_MESSAGE_META`에서 `messageType` 일치 row를 순회
3. `message.stats[messageId]` 갱신
   - `TOTAL_SUM`: `current + delta`
   - `TOTAL_MAX`: `max(current, delta)`
   - `TOTAL_MIN`: `min(current, delta)`
4. `GameMessageTrigger` publish로 mission runtime 구독자 notify
5. `AchieveManager`로 동일 이벤트 전달

runtime 반영:

- `SESSION_SUM`: `progress + delta`
- `SESSION_MAX`: `max(progress, delta)`
- `SESSION_MIN`: `min(progress, delta)`
- `TOTAL_*`: external reader(`message.stats[conditionMsgId]`)로 refresh

---

## E) Storage / Save Rules

`MissionStorage` 정본 필드:

- `schemaVersion` (기본 2)
- `dailyMissionStartUtcMs`
- `periodMissionStartUtcMs`
- `nextMissionUid`
- `runtimes: Dictionary<int, MissionRuntimeBase>`

runtime 저장 규칙:

- `missionId`, `conditionMsgId`, `missionUid`, `isWaiting`, `isCompleted`
- `periodKey`, `index`, `progressValue`

---

## F) Daily Clock Rules

- anchor: `MissionStorage.dailyMissionStartUtcMs`
- period index:
  - `floor(max(0, serverNowUtcMs - dailyMissionStartUtcMs) / 86400000)`
- daily key: `daily:{index}`
- cycle 전환 시 daily runtime set은 재구성된다.

---

## G) Period Clock Rules

- cycle days: `10`
- anchor: `MissionStorage.periodMissionStartUtcMs`
- cycle index:
  - `floor(max(0, serverNowUtcMs - periodMissionStartUtcMs) / (10 * 86400000))`
- cycle key: `period:{index}`
- 활성화 규칙:
  - `day == 1` -> 초기화 직후 ACTIVE
  - `day == n` -> `(n - 1)`일 경과 후 ACTIVE
- cycle 전환 시 모든 period runtime을 WAIT로 재생성하고 day 규칙으로 재활성화한다.

---

## H) UID Rules

- `missionUid`는 scheduler가 발급하는 증가형 int
- 발급 시 사용 중 UID는 건너뛴다

---

## Related

- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [01-policy](../01-policy/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [45-meta-message-system/11-game-message-trigger](../../45-meta-message-system/11-game-message-trigger/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
