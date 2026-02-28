# 31-ad-provider-google-mobile-ads


이 문서는 Google Mobile Ads 기반 provider adapter의 경계와 규약을 정의한다.

- **기준 SDK**: Google Mobile Ads Unity Plugin **v11** (Android `play-services-ads:25.0.0` / iOS `Google-Mobile-Ads-SDK ~> 13.0`)


---


## Scope

- Google Mobile Ads SDK 초기화 래핑
- placement별 load/show/hide API 매핑
- SDK 콜백을 공통 provider 이벤트로 변환
- test ad unit / test device 기반 smoke test 규칙 유지

비범위:
- 상위 게임 로직
- 보상 지급 계산
- 라이브 ad ops 운영 콘솔


---


## Hard Rules

- 상위 로직은 `GoogleMobileAds.*` 타입을 직접 참조하지 않는다
- provider는 `ADVERTISE.Provider == GOOGLE_MOBILE_ADS` row의 ad unit id만 처리한다
- test mode와 live mode를 명확히 구분한다
- Ad Inspector / test ad unit 검증은 허용하지만, 라이브 unit 반복 테스트는 금지한다


---


## Provider Interface (개념)

- `InitializeAsync(ct)`
- `LoadAsync(advertiseId, format, adUnitId, ct)`
- `ShowAsync(advertiseId, format, ct)`
- `Hide(advertiseId, format)` — 동기. Banner hide는 즉시 완료되므로 비동기 불필요.
- callbacks:
  - `OnLoaded`
  - `OnFailedToLoad`
  - `OnOpened`
  - `OnClosed`
  - `OnRewardEarned`


---


## Notes

- 현재 레포에는 Google Mobile Ads 플러그인이 UnityExample 프로젝트에 이미 들어와 있다.
- 샘플 구현은 SDK 직접 호출을 provider 파일로 한정해, 이후 mediation 또는 다른 provider로 바꿔도 AdManager 상위 API를 유지해야 한다.
- Rewarded 지급은 provider가 하지 않고, AdManager가 `OnRewardEarned`를 받아 RewardManager로 위임한다.
