# 26-remote-config-system — Overview

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 원격 설정/서버 시각 동기화 계층 개요다.

핵심:
- `RemoteConfigManager`가 Firebase callable `getRemoteConfig`를 호출한다.
- `RemoteConfigSnapshot`에 `serverNowUtcMs`, `minVersion`, `currentVersion`를 유지한다.
- 시즌/업적/구매 시간 판정은 `RemoteConfigManager.TryGetServerNowUtcMs(...)`만 사용한다.
- legacy `MissionClockSnapshot`/`TimeManager`는 사용하지 않는다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [03-ssot](../03-ssot/SKILL.md) | 타입/초기화/저장 정본 |
| [10-remote-config-manager](../10-remote-config-manager/SKILL.md) | RemoteConfigManager 설계 |

---

## Related

- [11-mobile-application](../../11-mobile-application/SKILL.md)
- [23-firebase-manager](../../23-firebase-manager/SKILL.md)
- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [30-purchase-system](../../30-purchase-system/00-overview/SKILL.md)
- [50-leaderboard](../../50-leaderboard/00-overview/SKILL.md)
