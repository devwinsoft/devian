# 48-mission-system — Overview

Status: ACTIVE  
AppliesTo: v10

MobileSystem 샘플의 Mission(일일/업적) 시스템 개요다.

- Mission row(`MISSION_DAY`, `MISSION_ACHIEVE`)는 조건 타입/연산자를 직접 가지지 않고 `missionStatId`를 가진다.
- `MISSION_STAT`가 `missionStatId -> (statType, opType)`를 정의한다.
- MissionManager는 `MissionTriggerSystem`과 `MissionMessageSystem`을 소유한다.
- MissionManager는 `MissionScheduler`를 통해 runtime 생성/복구/정리를 수행한다.
- `MissionTriggerSystem` 입력 타입은 `MISSION_STAT_TYPE`이다.
- 외부 trigger 입력 진입점은 `MissionManager.Notify(...)`다.
- 외부 message 구독 진입점은 `MissionManager.Subcribe(...)` 계열 헬퍼다.
- trigger 입력이 들어오면 MissionManager가 먼저 `MissionStorage.stats[missionStatId]`를 갱신하고, 그 다음 runtime notify를 수행한다.
- `DAY` runtime은 `progressValue`를 자체 보유/저장한다.
- `ACHIEVE` runtime 진행도의 정본은 `MissionStorage.stats[string missionStatId]`다.
- `ACHIEVE`의 `periodKey`는 고정 `"once"`이며 저장 payload에는 포함하지 않는다.
- timed mission(daily)은 `dailyMissionStartUtcMs` + 서버 시각 추정으로 period를 계산한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | 테이블/저장/트리거/레벨업 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-mission-manager](../10-mission-manager/SKILL.md) | MissionManager 설계 |
| [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md) | MissionTriggerSystem 설계 |
| [12-mission-storage](../12-mission-storage/SKILL.md) | MissionStorage 저장/복구 규약 |
| [13-mission-runtime](../13-mission-runtime/SKILL.md) | MissionRuntime 진행도/완료/레벨업 규약 |
| [14-mission-factory](../14-mission-factory/SKILL.md) | MissionRuntimeFactory 생성/복구 규약 |
| [15-mission-scheduler](../15-mission-scheduler/SKILL.md) | MissionScheduler lifetime 규약 |
| [16-mission-message-system](../16-mission-message-system/SKILL.md) | MissionMessageSystem notify 규약 |

---

## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [50-leaderboard](../../50-leaderboard/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
