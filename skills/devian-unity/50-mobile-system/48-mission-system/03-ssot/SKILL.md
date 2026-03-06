# 03-ssot — 48-mission-system

Status: ACTIVE  
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `MISSION_DAY`, `MISSION_ACHIEVE`, `MISSION_STAT` 스키마
- `MISSION_STAT_TYPE`, `MISSION_OP_TYPE`, `MISSION_MESSAGE` 사용 규약
- Mission trigger/update/runtime binding 규칙
- Mission 저장 구조(`MissionStorage`, runtime, stats) 규칙
- `ACHIEVE` level-up 재구독 규칙

---

## A) Core Terms

- `missionId`: daily는 미션 ID, achieve는 그룹 ID
- `missionType`: `MISSION_TYPE` (`DAY`, `ACHIEVE`)
- `missionStatId`: mission row가 참조하는 stat key
- `statType`: trigger key (`MISSION_STAT_TYPE`)
- `opType`: stat 누적 연산 (`MISSION_OP_TYPE`)
- `conditionValue`: 목표값(`CBigInt`)
- `periodKey`:
  - daily: `day:{dailyPeriodIndex}`
  - achieve: 고정 `once`
- `missionUid`: runtime 식별 `int`
- `mission stats`: `MissionStorage.stats[string missionStatId]`

---

## B) Table Schema

### `MISSION_DAY`

| field | type | note |
|---|---|---|
| `missionId` | string (pk) | daily 미션 ID |
| `isActive` | bool | 운영 토글 |
| `fixed` | bool | daily 선택 우선 포함 |
| `orderNum` | int | 정렬 기준(1-base) |
| `missionStatId` | string | `MISSION_STAT.missionStatId` FK |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키 |

### `MISSION_ACHIEVE`

| field | type | note |
|---|---|---|
| `index` | int (pk) | row pk |
| `missionId` | string | 업적 그룹 ID |
| `isActive` | bool | 운영 토글 |
| `orderNum` | int | 정렬 기준(1-base) |
| `missionStatId` | string | `MISSION_STAT.missionStatId` FK |
| `level` | int | 단계 |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | 보상 키 |

### `MISSION_STAT`

| field | type | note |
|---|---|---|
| `missionStatId` | string (pk) | stat 식별자 |
| `statType` | `MISSION_STAT_TYPE` | trigger 타입 |
| `opType` | `MISSION_OP_TYPE` | 누적 방식 |

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
- achievement는 group별 runtime 1개만 유지한다.

---

## D) Trigger + Stats Update

`MissionTriggerSystem` 정본 타입:

```csharp
MessageSystem<int, MISSION_STAT_TYPE>
```

외부 진입점은 `MissionManager.Notify(statType, delta)`다.

trigger 처리 순서:

1. `MISSION_STAT`에서 `statType` 일치 row를 순회
2. `stats[missionStatId]` 갱신
   - `SUM`: `current + delta`
   - `MAX`: `max(current, delta)`
3. 내부 `MissionTriggerSystem`으로 runtime 구독자 notify

runtime 반영:

- `DAY`: runtime이 `progressValue`를 직접 갱신
  - `MAX`: `max(progressValue, delta)`
  - `SUM`: `min(conditionValue, progressValue + delta)`
- `ACHIEVE`: runtime이 delta를 직접 누적하지 않고, reader로 `stats[missionStatId]`를 읽어 동기화

---

## E) Level-Up (ACHIEVE)

`ClaimAsync` 성공 후 다음 level row가 있으면 같은 runtime(`missionUid` 유지)으로 level-up 한다.

필수 순서:

1. 기존 statType 구독 해지
2. `level` 갱신, `isCompleted = false`
3. 새 row의 `missionStatId/statType/opType/conditionValue` 바인딩
4. 새 `missionStatId` reader(`stats[missionStatId]`) 연결
5. 새 statType으로 재구독

주의:
- `LevelUp`에서 progress를 수동 set 하지 않는다.
- progress source of truth는 `stats[missionStatId]`다.

---

## F) Storage / Save Rules

`MissionStorage` 정본 필드:

- `schemaVersion` (기본 2)
- `dailyMissionStartUtcMs`
- `clockSnapshot`
- `clockReceivedAtClientUtcMs`
- `nextMissionUid`
- `runtimes: Dictionary<int, MissionRuntimeBase>`
- `stats: Dictionary<string, CBigInt>`

runtime 저장 규칙:

- 공통: `missionType`, `missionId`, `missionStatId`, `missionUid`, `isCompleted`
- `DAY`: `periodKey`, `index`, `progressValue` 저장
- `ACHIEVE`: `level` 저장, `periodKey`/`progressValue`는 저장하지 않음

deserialize 규칙:

- `ACHIEVE.periodKey = "once"` 고정
- `ACHIEVE.progressValue = 0`로 시작하고 reader로 sync
- legacy fallback(`missionKind`, legacy progress seed)은 사용하지 않는다

---

## G) Daily Clock Rules

- anchor: `MissionStorage.dailyMissionStartUtcMs`
- period index:
  - `floor(max(0, estimatedServerNowUtcMs - dailyMissionStartUtcMs) / 86400000)`
- daily key: `day:{index}`
- 현재 sync 시각과 anchor 차이가 7일 초과면 anchor를 현재 서버 시각으로 재설정한다.

---

## H) Grant/Uid Rules

- `missionUid`는 scheduler가 발급하는 증가형 int
- 발급 시 사용 중 UID는 건너뛴다
- achievement level-up은 새 UID를 발급하지 않는다

---

## Related

- [01-policy](../01-policy/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
