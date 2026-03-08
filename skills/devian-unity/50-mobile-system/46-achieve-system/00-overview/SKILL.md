# 46-achieve-system — Overview

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 Achieve 시스템 개요다.
`AchieveManager`가 업적 runtime + 플랫폼 업적 연동을 함께 담당한다.

이 스킬 그룹 책임:
- 업적 runtime 생성/복구/level-up (`ACHIEVE_ONCE`, `ACHIEVE_PASS` table)
- 업적 claim/reward/save orchestration
- 내부 업적 ID -> 플랫폼 업적 ID 매핑
- 플랫폼 업적 Unlock/Sync
- 신규 달성 이벤트(`OnAchievementUnlocked`) 발행
- runtime 타입 분기(`ACHIEVE_TYPE`: `ONCE`, `PASS`)

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰/API 규약 |
| [03-ssot](../03-ssot/SKILL.md) | 테이블/매핑/저장 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영/테스트/DoD |
| [10-achieve-manager](../10-achieve-manager/SKILL.md) | AchieveManager 설계 |
| [11-achieve-platform-apple](../11-achieve-platform-apple/SKILL.md) | Apple(Game Center) 연동 |
| [12-achieve-platform-google](../12-achieve-platform-google/SKILL.md) | Google(GPGS v2) 연동 |
| [13-achieve-runtime](../13-achieve-runtime/SKILL.md) | AchieveRuntimeBase/Once/Pass 규약 |
| [14-achieve-storage](../14-achieve-storage/SKILL.md) | AchieveStorage/SaveData 규약 |
| [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md) | AchieveMessageTrigger notify 규약 |

---

## Related

- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [50-leaderboard](../../50-leaderboard/00-overview/SKILL.md)
