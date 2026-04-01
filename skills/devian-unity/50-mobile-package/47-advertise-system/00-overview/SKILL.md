# 47-advertise-system — Overview


Status: ACTIVE
AppliesTo: v10


> Routing(키워드→스킬)은 중앙 정본을 따른다: `skills/devian/00-overview/SKILL.md`


MobilePackage 샘플에서 In-App Ads(배너/전면/리워드/앱 오픈) 시스템을 정의한다.

- AdsManager는 `ADVERTISE` 테이블을 읽고 placement/format/provider 설정을 해석한다.
- Rewarded Ad의 실제 지급 실행은 RewardManager(49-reward-system)에 `reward_group_id`를 전달해 위임한다.
- 광고 테스트의 기본 경로는 MockAdProvider이며, 실 SDK 테스트는 제한된 수동 smoke test로만 수행한다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 광고 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | `ADVERTISE` / reward_group_id / provider 정본 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영/테스트/DoD |
| [30-ads-manager](../30-ads-manager/SKILL.md) | AdsManager(오케스트레이터) |
| [31-ad-provider-google-mobile-ads](../31-ad-provider-google-mobile-ads/SKILL.md) | Google Mobile Ads provider 규약 |
| [32-ad-format-banner](../32-ad-format-banner/SKILL.md) | Banner 광고 규약 |
| [33-ad-format-interstitial](../33-ad-format-interstitial/SKILL.md) | Interstitial 광고 규약 |
| [34-ad-format-rewarded](../34-ad-format-rewarded/SKILL.md) | Rewarded 광고 규약 |
| [35-ad-format-app-open](../35-ad-format-app-open/SKILL.md) | App Open 광고 규약 |
| [38-ad-mock-provider](../38-ad-mock-provider/SKILL.md) | Mock/Fake provider 테스트 규약 |
| [40-ad-ssv-firebase](../40-ad-ssv-firebase/SKILL.md) | Rewarded SSV Firebase 서버 구현 정본 |
| [41-ad-ssv-decisions](../41-ad-ssv-decisions/SKILL.md) | SSV 고정 결정값 |


---


## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [30-purchase-system](../../30-purchase-system/00-overview/SKILL.md)
- [50-mobile-package overview](../../00-overview/SKILL.md)
