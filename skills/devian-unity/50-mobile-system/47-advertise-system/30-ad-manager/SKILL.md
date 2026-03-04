# 30-ad-manager


AdManager는 `ADVERTISE` 테이블 기반으로 광고 placement를 해석하고,
provider 초기화/로드/표시를 오케스트레이션한다.
Rewarded 광고의 실제 지급 실행은 RewardManager에 위임한다.


---


## Singleton

```csharp
CompoSingleton<AdManager>.Instance
```

- Registry key: `AdManager`
- 다른 매니저에서 접근: `Singleton.Get<AdManager>()`


---


## Responsibilities (정본)

- `TB_ADVERTISE`에서 광고 설정 조회
- `row.Provider` 기준 provider 초기화/선택
- preload/show 상태 관리
- placement별 cooldown 검사
- NoAds/consent/unsupported platform gating
- Rewarded 성공 시 `RewardManager.ApplyRewardGroup(rewardGroupId)` 호출

비책임(금지):
- `TB_REWARD` 직접 조회
- 광고 수익 집계/분석
- 서버 검증/SSV 원장
- live ad unit 반복 테스트


---


## Dependencies (개념)

- provider: `31-ad-provider-google-mobile-ads` 또는 `38-ad-mock-provider`
- Reward 지급: `49-reward-system` (RewardManager)
- NoAds entitlement 참조: `30-purchase-system`


---


## Public API (설계)

- `InitializeAsync(ct)` → `Task<CommonResult>`
  - provider 초기화. Idempotent.
- `PreloadAsync(advertiseId, ct)` → `Task<CommonResult>`
  - `TB_ADVERTISE` row 기준 preload 수행
- `CanShow(advertiseId)` → `bool`
  - 활성 여부, cooldown, no-ads, readiness를 종합 판정
- `ShowAsync(advertiseId, skip, ct)` → `Task<CommonResult<AdShowResult>>`
  - 단일 광고 진입점
  - `skip=true`이면 광고 노출 없이 Reward만 즉시 지급 (`ProviderStatus=Skipped`)
  - format에 따라 show/hide 또는 one-shot show 수행
  - Rewarded는 성공 콜백 시 RewardManager 호출
- `HideBanner(advertiseId)` → `void`
  - banner placement hide. 동기 — provider의 `Hide()`를 호출한다.


### `AdShowResult` (설계)

- `AdvertiseId`
- `Format`
- `RewardGroupId`
- `RewardApplied` (`bool`)
- `ProviderStatus`


---


## ADVERTISE 통합 (TB_ADVERTISE 직접 참조)

AdManager가 Game 도메인 테이블을 직접 참조한다.

- `ResolveAdvertise(advertiseId)` → `TB_ADVERTISE.Get(advertiseId)`
- `AutoLoadAll()` → `TB_ADVERTISE.GetAll()`에서 `IsActive && AutoLoad` 필터링
- provider 선택: `row.Provider`
- provider용 ad unit id 선택:
  - `#if UNITY_IOS` → `IosAdUnitId`
  - `#elif UNITY_ANDROID` → `AndroidAdUnitId`


---


## Rewarded Sequence (정본)

1. `ShowAsync(advertiseId, skip, ct)`
2. row 조회: `TB_ADVERTISE.Get(advertiseId)`
3. `skip=true`이면 → 광고 없이 `SkipAndReward` → 즉시 반환
4. `Format == REWARDED` 확인
5. provider show
6. `reward earned` 콜백 수신
7. 현재 show cycle에서 아직 미지급이면 `RewardManager.ApplyRewardGroup(row.RewardGroupId)` 호출
8. close/final result 반환


---


## Implementation Location (3-path mirror, planned)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/01-policy](../../../01-policy/SKILL.md) §SSOT 원칙

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Ads/AdManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Ads/AdManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Ads/AdManager.cs`


---


## Related

- [03-ssot](../03-ssot/SKILL.md)
- [31-ad-provider-google-mobile-ads](../31-ad-provider-google-mobile-ads/SKILL.md)
- [34-ad-format-rewarded](../34-ad-format-rewarded/SKILL.md)
- [10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
