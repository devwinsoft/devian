# 03-ssot — 48-mission-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `MISSION_DAILY`, `MISSION_WEEKLY`, `GAME_MESSAGE` 스키마
- `MISSION_TYPE`, `GAME_MESSAGE_*`, `MISSION_MESSAGE_TYPE` 사용 규약
- Mission trigger/update/runtime binding 규칙
- Mission 저장 구조(`MissionStorage`, daily/period runtime) 규칙

업적(`ACHIEVE`) runtime/claim/save 정본은
[46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)에서 관리한다.

---

## A) Core Terms

- `mission_id`: 미션 ID
- `missionType`: `MISSION_TYPE.DAILY` / `MISSION_TYPE.WEEKLY`
- `condition_msg_id`: mission row가 참조하는 `GAME_MESSAGE.message_id`
- `messageType`: trigger key (`GAME_MESSAGE_TYPE`)
- `saveType`: stat 누적 연산 (`GAME_MESSAGE_SAVE_TYPE`)
- `condition_op`: 비교 연산 (`GAME_MESSAGE_OP_TYPE`)
- `condition_value`: 목표값(`CBigInt`)
- `periodKey`: runtime 주기 식별자 (`daily:{index}`, `weekly:{index}`)
- `weeklyDayGroupKey`: `MISSION_WEEKLY.day` (weekly runtime 활성화 그룹 키)
- `missionUid`: runtime 식별 `int`
- `message stats`: `GameMessageStorage.stats[string message_id]`

---

## B) Table Schema

### `MISSION_DAILY`

| field | type | note |
|---|---|---|
| `mission_id` | string (pk) | 미션 ID |
| `is_active` | bool | 운영 토글 |
| `fixed` | bool | daily 선택 우선 포함 |
| `order_num` | int | 정렬 기준(1-base) |
| `condition_msg_id` | string | `GAME_MESSAGE.message_id` FK |
| `condition_op` | `GAME_MESSAGE_OP_TYPE` | 조건 비교 타입 |
| `condition_value` | `class:CBigInt` | 목표값 |
| `reward_group_id` | string | 보상 키 |

규칙:
- `missionType` 필드는 없다(`MISSION_DAILY` 자체가 DAILY 타입).

### `MISSION_WEEKLY`

| field | type | note |
|---|---|---|
| `mission_id` | string (pk) | 미션 ID |
| `is_active` | bool | 운영 토글 |
| `day` | int | 활성화 day (1~7) |
| `condition_msg_id` | string | `GAME_MESSAGE.message_id` FK |
| `condition_op` | `GAME_MESSAGE_OP_TYPE` | 조건 비교 타입 |
| `condition_value` | `class:CBigInt` | 목표값 |
| `reward_group_id` | string | 보상 키 |

규칙:
- `missionType`, `fixed`, `order_num` 필드는 없다.
- `day`는 `1~7` 범위를 사용한다.
- `day`는 weekly runtime의 group key다. 동일 `day` row는 같은 activation bucket으로 처리한다.

### `GAME_MESSAGE`

| field | type | note |
|---|---|---|
| `message_id` | string (pk) | message 식별자 |
| `messageType` | `GAME_MESSAGE_TYPE` | trigger 타입 |
| `saveType` | `GAME_MESSAGE_SAVE_TYPE` | 누적 방식 |

정본 source:
- `input/Domains/Game/MetaTable.xlsx`
- `input/Domains/Game/ENUM_GAME.json`

---

## C) Runtime State

`MissionRuntimeState` enum (lifecycle 순서):

| 값 | int | 설명 |
|---|---|---|
| `NONE` | 0 | runtime 미존재 (필드에 저장 안 됨) |
| `WAIT` | 1 | 대기 (period day 미도달) |
| `ACTIVE` | 2 | 진행 중 |
| `CLAIMABLE` | 3 | 보상 수령 가능 (파생 상태, 필드에 저장 안 됨) |
| `COMPLETED` | 4 | 완료 |

- `state` 필드: `WAIT`, `ACTIVE`, `COMPLETED`만 저장한다.
- `CLAIMABLE`은 `state == ACTIVE && progress가 조건 충족` 시 `GetState()`가 반환하는 파생 상태다.

규칙:
- `saveType == NONE` row는 runtime을 생성하지 않는다.
- weekly runtime은 초기화/리셋 직후 기본 상태가 WAIT다.

---

## D) Trigger + Stats Update

`GameMessageTrigger` 정본 타입:

```csharp
BaseTrigger<int, GAME_MESSAGE_TYPE>
```

외부 진입점은 `GameMessageManager.Notify(messageType, delta)`다.

trigger 처리 순서:

1. `GameMessageManager.Notify` 호출
2. `TB_GAME_MESSAGE`에서 `messageType` 일치 row를 순회
3. `message.stats[message_id]` 갱신
   - `TOTAL_SUM`: `current + delta`
   - `TOTAL_MAX`: `max(current, delta)`
   - `TOTAL_MIN`: `min(current, delta)`
4. `GameMessageTrigger` publish로 mission runtime 구독자 notify
5. `AchieveManager`로 동일 이벤트 전달

runtime 반영:

- `SESSION_SUM`: `progress + delta`
- `SESSION_MAX`: `max(progress, delta)`
- `SESSION_MIN`: `min(progress, delta)`
- `TOTAL_*`: external reader(`message.stats[condition_msg_id]`)로 refresh

---

## E) Storage / Save Rules

`MissionStorage` 정본 필드:

- `schemaVersion` (기본 2)
- `dailyMissionStartUtcMs`
- `weeklyMissionStartUtcMs`
- `nextMissionUid`
- `runtimes: Dictionary<int, MissionRuntimeBase>`

runtime 저장 규칙:

- `mission_id`, `missionUid`, `state`
- `periodKey`, `index`, `progressValue`
- `condition_msg_id`는 저장하지 않는다(`mission_id`로 테이블에서 조회)

---

## F) Daily Clock Rules

- anchor: `MissionStorage.dailyMissionStartUtcMs`
- period index:
  - `floor(max(0, serverNowUtcMs - dailyMissionStartUtcMs) / 86400000)`
- daily key: `daily:{index}`
- cycle 전환 시 daily runtime set은 재구성된다.

---

## G) Weekly Clock Rules

- cycle days: `10`
- anchor: `MissionStorage.weeklyMissionStartUtcMs`
- cycle index:
  - `floor(max(0, serverNowUtcMs - weeklyMissionStartUtcMs) / (10 * 86400000))`
- cycle key: `weekly:{index}`
- 활성화 규칙:
  - `day == 1` -> 초기화 직후 ACTIVE
  - `day == n` -> `(n - 1)`일 경과 후 ACTIVE
- cycle 전환 시 모든 weekly runtime을 WAIT로 재생성하고 day 규칙으로 재활성화한다.

---

## H) UID Rules

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
