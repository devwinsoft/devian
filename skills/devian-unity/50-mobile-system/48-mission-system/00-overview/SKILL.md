# 48-mission-system — Overview

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 Mission 시스템 개요다.
현재 Mission 시스템은 `DAY` 미션만 담당한다.

- Mission row는 `MISSION_DAY`만 사용한다.
- 조건 타입/연산자 정본은 `MISSION_STAT(missionStatId, statType, opType)`다.
- MissionManager는 `MissionMessageTrigger`과 `MissionScheduler`를 소유한다.
- daily runtime 구독은 `GameMessageTrigger`를 직접 사용한다.
- MissionManager는 `MissionScheduler`를 통해 daily runtime 생성/복구/정리를 수행한다.
- 외부 trigger 입력 진입점은 `MissionManager.Notify(...)`다.
- 외부 message 구독 진입점은 `MissionManager.Subcribe(...)` 계열 헬퍼다.
- `DAY` runtime은 `progressValue`를 자체 보유/저장한다.
- 업적(`ACHIEVE`) runtime/claim/저장은 [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md) 책임이다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | 테이블/저장/트리거 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-mission-manager](../10-mission-manager/SKILL.md) | MissionManager 설계 |
| [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md) | GameMessageTrigger 설계 |
| [12-mission-storage](../12-mission-storage/SKILL.md) | MissionStorage 저장/복구 규약 |
| [13-mission-runtime](../13-mission-runtime/SKILL.md) | MissionRuntime 진행도/완료 규약 |
| [14-mission-factory](../14-mission-factory/SKILL.md) | MissionRuntimeFactory 생성/복구 규약 |
| [15-mission-scheduler](../15-mission-scheduler/SKILL.md) | MissionScheduler lifetime 규약 |
| [16-mission-message-trigger](../16-mission-message-trigger/SKILL.md) | MissionMessageTrigger notify 규약 |

---

## Related

- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
