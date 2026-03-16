# 52-push-system — Overview

Status: ACTIVE
AppliesTo: v10

> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`

MobilePackage 샘플의 Push 시스템 개요다.
`PushManager`가 푸시 토큰 관리, 토픽 구독/해제, 로컬 알림 스케줄링을 담당한다.

이 스킬 그룹 책임:
- FCM/APNs 토큰 등록/갱신/삭제
- 토픽 구독/해제
- 로컬 푸시 알림 스케줄/취소
- 플랫폼별 Provider 추상화 (Apple/Google)

문서 중복 방지 라우팅:
- 정책/모듈 경계는 `01-policy`
- 통합 SSOT/핵심 합의는 `03-ssot`
- PushManager 설계/API는 `10-push-manager`
- Apple(APNs) 플랫폼 구현은 `11-push-provider-apple`
- Google(FCM) 플랫폼 구현은 `12-push-provider-google`
- Apple(APNs + FCM iOS) 인프라 셋업은 `20-push-setup-apple`
- Google(FCM Android) 인프라 셋업은 `21-push-setup-google`

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰/API 규약 |
| [03-ssot](../03-ssot/SKILL.md) | 토큰/토픽/로컬알림/Provider 정본 |
| [10-push-manager](../10-push-manager/SKILL.md) | PushManager 설계 |
| [11-push-provider-apple](../11-push-provider-apple/SKILL.md) | Apple(APNs) 연동 |
| [12-push-provider-google](../12-push-provider-google/SKILL.md) | Google(FCM) 연동 |
| [20-push-setup-apple](../20-push-setup-apple/SKILL.md) | Apple(APNs + FCM iOS) 인프라 셋업 |
| [21-push-setup-google](../21-push-setup-google/SKILL.md) | Google(FCM Android) 인프라 셋업 |

---

## Related

- [47-advertise-system](../../47-advertise-system/00-overview/SKILL.md)
- [45-game-message-system](../../45-game-message-system/00-overview/SKILL.md)
- [Root SSOT](../../../../devian/10-module/03-ssot/SKILL.md)
- [Unity SSOT](../../../03-ssot/SKILL.md)
- [Devian Index](../../../../devian/SKILL.md)
