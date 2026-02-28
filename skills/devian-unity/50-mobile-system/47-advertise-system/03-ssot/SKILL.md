# 03-ssot — 47-advertise-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

광고 관련 규칙의 단일 SSOT는 이 문서다.

- `ADVERTISE` 테이블 스키마
- `advertiseId` / `rewardGroupId` / 포맷 규칙
- AdManager의 책임과 provider 분리 규칙
- Rewarded 광고의 지급 위임 규칙

비정본(이 문서에서 다루지 않음):
- RewardData 스키마/보상 해석
  - `49-reward-system`
- 결제 NoAds entitlement 원장/복구
  - `30-purchase-system`


---


## A) Core Terms (정본)

- `advertiseId`: 광고 placement의 논리 키. `ADVERTISE` 테이블 PK.
- `format`: 광고 포맷. `ADVERTISE_FORMAT`
- `provider`: 광고 SDK 구현체 키. `ADVERTISE_PROVIDER`
- `rewardGroupId`: Rewarded 광고가 성공적으로 완료됐을 때 RewardManager에 전달하는 보상 그룹 키
- `show cycle`: 한 번의 show 시도 단위. Rewarded는 show cycle당 최대 1회만 지급한다.
- `MockAdProvider`: 네트워크 없이 scripted callback으로 동작하는 테스트용 provider


---


## B) Content Source of Truth (정본)

광고 메타데이터 정본은 Game 도메인 안의 `ADVERTISE` 테이블이다.

- 위치: `input/Domains/Game/*.xlsx` 중 `ADVERTISE` sheet를 포함한 파일
- 현재 샘플 파일: `input/Domains/Game/AdvertiseTable.xlsx`
- sheet/table 정본 이름: `ADVERTISE`
- 생성 대상: `TB_ADVERTISE`
- **도메인 소속**: `com.devian.domain.game` (기존 `TB_REWARD`와 동일)
- **빌더 등록**: 추가 등록 불필요. `input/input_common.json`의 `domains.Game.tableFiles=["*.xlsx"]`에 이미 포함된다.

AdManager는 `TB_ADVERTISE`를 직접 참조하여 placement 설정을 읽는다.


---


## C) `ADVERTISE` 테이블 스키마 (정본)

`ADVERTISE`는 1행=1광고 placement 구조다.

| 필드 | 타입 | Row 3 옵션 | 설명 |
|------|------|-----------|------|
| `AdvertiseId` | string | pk | 광고 placement 논리 키 |
| `Format` | enum:ADVERTISE_FORMAT | | `BANNER` / `INTERSTITIAL` / `REWARDED` / `APP_OPEN` |
| `Provider` | enum:ADVERTISE_PROVIDER | | `GOOGLE_MOBILE_ADS` / `MOCK` |
| `RewardGroupId` | string | | Rewarded 성공 시 지급할 보상 그룹 키 |
| `IsActive` | bool | | 활성 여부 |
| `AutoLoad` | bool | | 초기화 후 자동 preload 여부 |
| `CooldownSec` | int | | 동일 placement 재표시 최소 간격(초) |
| `AndroidAdUnitId` | string | | Android ad unit id |
| `IosAdUnitId` | string | | iOS ad unit id |


### C-1) Format 규칙

- `Format=REWARDED`이면 `RewardGroupId`는 필수다.
- `Format=BANNER|INTERSTITIAL|APP_OPEN`이면 `RewardGroupId`는 비워둔다.
- `Provider`는 현재 `GOOGLE_MOBILE_ADS` 또는 `MOCK`만 허용한다.
- `IsActive=false`면 로드/표시 대상에서 제외한다.
- `CooldownSec <= 0`이면 쿨다운 없음으로 해석할 수 있다.


### C-2) 광고 ID 해석 규칙

- 상위 로직은 `advertiseId`만 사용한다.
- 플랫폼별 ad unit id(`AndroidAdUnitId`, `IosAdUnitId`)는 provider/AdManager 레이어 내부에서만 해석한다.
- 상위 로직이 ad unit id를 직접 알거나 분기하지 않는다.


---


## D) Rewarded 지급 경로 (정본)

Rewarded 광고는 아래 순서를 따른다.

1. 상위 로직이 `ShowAsync(advertiseId, skip, ct)`를 호출한다.
2. AdManager가 `TB_ADVERTISE.Get(advertiseId)`로 row를 읽는다.
3. `skip=true`이면 광고 없이 `SkipAndReward`로 Reward만 즉시 지급하고 반환한다.
4. provider가 rewarded 광고를 표시한다.
5. provider에서 `reward earned` 콜백이 오면, AdManager가 해당 show cycle의 중복 여부를 확인한다.
6. `RewardGroupId`가 유효하면 `RewardManager.ApplyRewardGroup(rewardGroupId)`를 호출한다.
7. show cycle 종료 결과를 반환한다.

정본 규칙:
- 광고 시스템은 `RewardData[]`를 직접 생성하지 않는다.
- `reward earned` 이전에 선지급하지 않는다.
- 동일 show cycle에서 중복 reward 콜백이 와도 1회만 지급한다.


연관:
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
- [10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)


---


## E) AdManager 책임 (정본)

- `TB_ADVERTISE` 조회
- provider 초기화/선택
- preload/show 상태 관리
- format별 gating(no-ads, consent, unsupported platform, cooldown)
- Rewarded 성공 시 RewardManager 호출

AdManager의 비책임:
- 보상 내용 해석(`TB_REWARD` 직접 조회 금지)
- 결제 entitlement 저장/복구
- 실광고 테스트 자동화
- 공급자 SDK 타입을 상위 로직으로 노출


---


## F) Provider 분리 규칙 (정본)

- provider는 `IAdProvider` 류의 공통 인터페이스 뒤로 숨긴다.
- Google Mobile Ads SDK 의존은 provider 구현 파일에만 둔다.
- MockAdProvider는 동일 인터페이스를 구현해 Editor/CI 테스트 기본값으로 사용한다.


---


## G) Non-goals

- 광고 수익 분석/BI 파이프라인
- mediation waterfall 설정 UI
- live ops A/B 실험 도구


---


## H) Rewarded SSV (Server-Side Verification) 참조

AdMob SSV를 통해 Rewarded 광고 완료를 서버에서 검증하고 감사 로그를 기록한다.

- 보상 지급 방식: **클라이언트 즉시 지급** (현재 방식 유지). SSV는 감사/사후 검증 용도.
- SSV 서버 구현 정본: [40-ad-ssv-firebase](../40-ad-ssv-firebase/SKILL.md)
- SSV 고정 결정값: [41-ad-ssv-decisions](../41-ad-ssv-decisions/SKILL.md)

핵심 흐름:
1. 클라이언트: Rewarded 로드 후 `ServerSideVerificationOptions`(userId, customData) 설정
2. 유저 광고 시청 완료 → 클라이언트 즉시 보상 지급 (§D 경로 그대로)
3. AdMob → Firebase HTTP endpoint로 SSV 콜백 전송
4. 서버: ECDSA 서명 검증 → Firestore 감사 로그 기록
