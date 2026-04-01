# 50-leaderboard — Overview

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 Leaderboard 시스템 범위를 정의한다.

- Apple(Game Center) / Google Play Games Services(GPGS v2) 점수 제출
- player snapshot 조회(내 점수/랭크 유무)
- 시즌 전환 보상 평가/지급(LEADERBOARD_REWARD + reward_group_id)

업적 Unlock/Sync는 `46-achieve-system`으로 분리되었다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰/API 규약 |
| [03-ssot](../03-ssot/SKILL.md) | LEADERBOARD/LEADERBOARD_REWARD/season reward 저장 SSOT |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영/테스트/DoD |
| [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md) | LeaderboardManager(점수 제출 + snapshot 조회) |
| [11-leaderboard-platform-apple](../11-leaderboard-platform-apple/SKILL.md) | Apple(Game Center) adapter |
| [12-leaderboard-platform-google](../12-leaderboard-platform-google/SKILL.md) | Google(GPGS v2) adapter |
| [13-leaderboard-season-reward-manager](../13-leaderboard-season-reward-manager/SKILL.md) | 시즌 전환 보상 평가/지급 |
| [14-leaderboard-season-reward-storage](../14-leaderboard-season-reward-storage/SKILL.md) | processedClaims 저장 모델/codec |

---

## Related

- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
- [11-mobile-application](../../11-mobile-application/SKILL.md)
- [MobilePackage Overview](../../00-overview/SKILL.md)
- [34-account-login-apple](../../20-account-system/34-account-login-apple/SKILL.md)
- [36-account-login-gpgs](../../20-account-system/36-account-login-gpgs/SKILL.md)
