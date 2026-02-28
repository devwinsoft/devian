# 47-advertise-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Devian의 인앱 광고 모듈(클라이언트) 설계/코딩 규약을 정의한다.

- 현재 기본 공급자 SDK는 Google Mobile Ads를 기준으로 한다.
- 광고 설정 정본은 Game 도메인의 `ADVERTISE` 테이블로 둔다.
- Rewarded Ad의 실제 보상 지급은 RewardManager로 위임한다.


---


## Hard Rules


### 1) 광고 설정 정본은 `ADVERTISE` 테이블이다

- placement, format, rewardGroupId, ad unit id를 코드에 하드코딩하지 않는다.
- AdManager가 `TB_ADVERTISE`를 직접 참조하여 `advertiseId` 기준으로 설정을 해석한다.
- 포맷별 분기(`BANNER`, `INTERSTITIAL`, `REWARDED`, `APP_OPEN`)와 provider 선택은 테이블 값 기준으로만 수행한다.


정본 규칙: [03-ssot](../03-ssot/SKILL.md)


### 2) Rewarded 보상 지급은 RewardManager 단일 경로를 따른다

- Rewarded에서 실제 보상 적용은 `RewardManager.ApplyRewardGroupId(rewardGroupId)`만 사용한다.
- RewardManager가 `TB_REWARD`를 통해 보상 그룹을 해석하며, 광고 시스템은 보상 내용(`RewardData[]`)을 직접 계산하지 않는다.
- `reward earned` 콜백이 없는 종료/실패 경로에서는 지급하지 않는다.
- 동일 show cycle에서 보상은 최대 1회만 지급한다.


연관: [49-reward-system](../../49-reward-system/00-overview/SKILL.md)


### 3) RewardGroupId 규칙은 포맷에 따라 고정한다

- `REWARDED` 포맷은 `rewardGroupId`가 필수다.
- `BANNER`, `INTERSTITIAL`, `APP_OPEN`은 `rewardGroupId`를 비워둔다.
- 리워드 지급 여부를 런타임 분기/문자열 비교로 임의 추론하지 않는다. 포맷 + 테이블 설정으로만 판정한다.


### 4) 공급자 SDK 종속성은 provider 레이어에만 둔다

- 상위 로직(UI, gameplay, Mission, Reward, Purchase)은 `GoogleMobileAds.*` 타입을 직접 참조하지 않는다.
- SDK 초기화/로드/표시/콜백 해석은 provider adapter가 담당한다.
- AdManager는 provider의 공통 인터페이스만 사용한다.


### 5) 광고 실패는 non-fatal이다

- 광고 초기화 실패, load fail, no fill, show fail은 게임 진행을 막지 않는다.
- 미지원 플랫폼/Editor/설정 누락에서는 안전 실패(safe fail)로 종료한다.
- 예외 전파/로그 폭발로 앱 흐름을 깨뜨리지 않는다.


### 6) NoAds/정책 게이트를 우회하지 않는다

- Banner / Interstitial / App Open은 NoAds 활성 상태에서 표시하지 않는다.
- Rewarded는 사용자가 명시적으로 진입한 보상형 광고이므로 NoAds와 별도 정책으로 취급할 수 있다.
- consent, platform support, test-mode 여부를 통과하지 못하면 show 경로를 차단한다.


### 7) 실광고 반복 테스트를 기본 경로로 쓰지 않는다

- Editor/CI/로컬 개발 기본값은 MockAdProvider다.
- 실 SDK 테스트는 test ad unit 또는 test device 기반 수동 smoke test로만 수행한다.
- 라이브 ad unit 반복 호출/클릭/자동화 테스트는 금지한다.


정본 규칙: [09-ssot-operations](../09-ssot-operations/SKILL.md)


### 8) SSV 콜백은 ECDSA 서명 검증 필수

- AdMob SSV 콜백을 수신하는 Firebase HTTP endpoint는 반드시 ECDSA 서명을 검증해야 한다.
- 서명 검증 없이 보상을 기록하지 않는다.
- custom_data는 서버에서 파싱하여 uid/advertiseId/rewardGroupId를 추출한다.
- SSV는 감사/사후 검증 용도이며, 클라이언트 보상 지급 흐름을 차단하지 않는다.


정본 규칙: [40-ad-ssv-firebase](../40-ad-ssv-firebase/SKILL.md), [41-ad-ssv-decisions](../41-ad-ssv-decisions/SKILL.md)
