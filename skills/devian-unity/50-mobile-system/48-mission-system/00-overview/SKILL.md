# 48-mission-system — Overview


Status: ACTIVE
AppliesTo: v10


MobileSystem 샘플에서 Mission(일일/업적) 시스템을 정의한다.

- MissionManager는 `MISSION_*` 테이블을 읽고, **조건 평가/진행도/완료 판정**을 책임진다.
- MissionManager는 `MissionTriggerSystem`을 소유한다.
- MissionManager는 `MissionMessageSystem`을 소유한다.
- MissionManager는 `MissionScheduler`를 통해 `MissionRuntimeBase` 계열 runtime의 생성/복구/파기/lifetime을 관리한다.
- concrete runtime은 `conditionOp` 규칙에 따라 자신의 `ProgressValue`를 갱신하고 완료를 판정한다.
- MissionManager는 **보상 지급 조회/로컬 claim record/기간 전환 처리**를 책임진다. 실제 보상 적용은 RewardManager(49-reward-system)에 위임한다.
- 플랫폼 업적/리더보드 연동은 LeaderboardManager(50-leaderboard)가 책임진다.
- daily 기간 키는 **`MissionManager.Storage.dailyMissionStartUtcMs` + 현재 추정 서버 시각 기준**이다.
- timed mission(daily)은 로그인/동기화 시점의 `MissionClockSnapshot`으로 서버 시간을 보정하고, mission 저장소에 남긴 anchor를 기준으로 클라이언트가 계속 판정한다.
- Firebase는 mission 정보를 저장하지 않고, 서버 시계만 제공한다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | `MISSION_*` 테이블 스키마 + `MissionClockSnapshot` + grantId 규칙 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-mission-manager](../10-mission-manager/SKILL.md) | MissionManager 설계 |
| [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md) | MissionTriggerSystem 설계 |
| [12-mission-storage](../12-mission-storage/SKILL.md) | MissionStorage 저장/복구/SaveData 연동 규약 |
| [13-mission-runtime](../13-mission-runtime/SKILL.md) | MissionRuntime 진행도/완료/리셋 규약 |
| [14-mission-factory](../14-mission-factory/SKILL.md) | MissionRuntimeFactory 생성/복구 규약 |
| [15-mission-scheduler](../15-mission-scheduler/SKILL.md) | MissionScheduler lifetime 관리 규약 |
| [16-mission-message-system](../16-mission-message-system/SKILL.md) | MissionMessageSystem notify 규약 |


---


## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [50-leaderboard](../../50-leaderboard/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
