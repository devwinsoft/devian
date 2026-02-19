# 48-mission-system — Overview


Status: ACTIVE
AppliesTo: v10


MobileSystem 샘플에서 Mission(일일/주간/업적) 시스템을 정의한다.

- MissionManager는 `MISSION` 테이블을 읽고, **조건 평가/진행도/완료 판정**을 책임진다.
- MissionManager는 **보상 지급 조회/기록/개별 지급 기록/리셋**을 책임진다. 실제 보상 적용은 RewardManager(49-reward-system)에 위임한다.
- 플랫폼 업적/리더보드 연동은 LeaderboardManager(50-leaderboard)가 책임진다.
- daily/weekly 리셋 경계 및 기간 키(dailyKey/weeklyKey)는 **서버 시간 기준**이다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | `MISSION` 테이블 스키마 + grantId 규칙 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-mission-manager](../10-mission-manager/SKILL.md) | MissionManager 설계 |


---


## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [50-leaderboard](../../50-leaderboard/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
