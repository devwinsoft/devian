# 38-ad-mock-provider


MockAdProvider는 광고 SDK를 호출하지 않고 scripted callback으로 동작하는 테스트용 provider다.


---


## Purpose

- Editor/CI/로컬 개발 기본 provider
- 네트워크 없이 광고 흐름 재현
- 실광고 호출 없이 UI/상태 머신/보상 경계 검증


---


## Supported Scenarios

- initialize success / fail
- load success / no fill / timeout
- show success / show fail
- rewarded callback 발생
- close without reward
- duplicate reward callback (중복 지급 방어 검증용)


---


## Hard Rules

- 외부 네트워크 요청 금지
- 실 ad unit 사용 금지
- scripted scenario만으로 결과를 재현
- Rewarded 테스트에서 `RewardManager.ApplyRewardGroupId`가 1회만 호출되는지 검증 가능해야 한다


---


## Usage

- Editor와 CI는 기본적으로 mock provider를 사용한다
- 실 SDK smoke test가 필요할 때만 provider를 교체한다
- 자동화 테스트는 mock provider 기준으로 작성한다
