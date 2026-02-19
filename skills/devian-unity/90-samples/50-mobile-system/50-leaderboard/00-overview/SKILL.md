# 50-leaderboard — Overview


Status: ACTIVE
AppliesTo: v10


> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`


MobileSystem 샘플에서 Apple(Game Center) / Google Play Games Services(GPGS v2) 기반의
Leaderboard / Achievements 연동을 **설계 문서**로 정의한다.

이 스킬 그룹은 "플랫폼 연동 + 내부 ID 매핑 + 업적 동기화"까지만 책임진다.
업적 달성에 따른 Reward 지급은 별도 Reward 시스템(또는 게임 로직)이 처리하며,
Leaderboard는 **업적 달성 신호(event)** 만 제공한다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰/API 규약 |
| [03-ssot](../03-ssot/SKILL.md) | 내부 ID ↔ Apple/Google ID 매핑 및 업적→Reward 트리거 SSOT |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영/테스트/DoD(동기화/중복 방지) |
| [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md) | (설계) MobileSystem LeaderboardManager 샘플 |
| [11-leaderboard-platform-apple](../11-leaderboard-platform-apple/SKILL.md) | Apple(Game Center) 플랫폼 설계 |
| [12-leaderboard-platform-google](../12-leaderboard-platform-google/SKILL.md) | Google(GPGS v2) 플랫폼 설계 |


---


## Related


- [MobileSystem Overview](../../00-overview/SKILL.md)
- [34-account-login-apple](../../20-account-system/34-account-login-apple/SKILL.md)
- [36-account-login-gpgs](../../20-account-system/36-account-login-gpgs/SKILL.md)
- [Root SSOT](../../../../../devian/10-module/03-ssot/SKILL.md)
- [Unity SSOT](../../../../03-ssot/SKILL.md)
