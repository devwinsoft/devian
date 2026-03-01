# 30-purchase-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Devian의 인앱 결제 모듈(클라이언트) 설계/코딩 규약을 정의한다.


- 결제 SDK는 Unity IAP를 사용한다.
- 결제 검증/지급 결정은 **클라이언트가 아닌 서버(Cloud Functions)**가 담당한다.
- 상위 로직은 Store SKU / 스토어별 영수증 구조를 직접 알지 않는다(내부 ID만 사용).


---


## Hard Rules


### 1) 클라이언트는 "지급 결정"을 하지 않는다


- Unity IAP 콜백에서 "성공"이 와도 **즉시 지급 금지**
- 반드시 서버(Cloud Functions)의 `verifyPurchase` 결과를 기준으로 Entitlement를 반영한다.


정본 규칙: [03-ssot](../03-ssot/SKILL.md)


### 2) 내부 표준 ID만 상위 로직에 노출한다


- 상위 로직은 `internalProductId`만 사용한다.
- Store SKU(googlePlayProductId / appStoreProductId)는 카탈로그/매핑 레이어로만 취급한다.


정본 규칙: [03-ssot](../03-ssot/SKILL.md)


### 3) iOS Restore 지원


- iOS는 Restore(복원) 플로우를 제공해야 한다.
- Android는 UX상 "자동 복원/동기화" 패턴을 주로 사용해도 된다.
- 단, 최종 상태는 서버의 Entitlement 기준.


정본 규칙: [03-ssot](../03-ssot/SKILL.md)


### 4) Purchase의 실제 지급 실행은 RewardManager에 위임한다

- 서버 `verifyPurchase` 결과가 `GRANTED`일 때만 `rewardGroupId`를 RewardManager에 전달해 적용한다.
- `rewardGroupId`는 `internalProductId -> rewardGroupId` 매핑으로 얻는다(PurchaseManager가 `TB_PRODUCT` 테이블을 직접 조회하여 변환).
- `ALREADY_GRANTED`는 서버 멱등 결과이며, 클라에서 중복 적용을 시도하지 않는다.
- 멱등/기록/복구 정본은 Purchase 시스템이다. Reward는 지급 실행만 담당한다.

연관: [49-reward-system](../../49-reward-system/00-overview/SKILL.md)

### 5) 구매 전 인증 판정은 AccountManager 단일 책임이다

- PurchaseManager는 "로그인 여부"를 `FirebaseAuth.CurrentUser`로 직접 판정하지 않는다.
- 구매 가능 여부는 AccountManager의 로그인 상태 API(`IsPurchaseLoginReady` 등)로 판단한다.
- Android 정책: GPGS silent 인증이 이미 성립한 경우, 구매 진입 시 AccountManager가 Google 로그인(Firebase 연동)을
  자동 보정해 구매 가능 상태로 승격할 수 있다(사용자 명시 로그인 버튼 없이 가능).
- Firebase Auth context(`context.auth.uid`)는 서버 `verifyPurchase`에서 최종 검증한다.

### 6) 상품 종류별 구매 제한 정책 (현재)

- `Consumable`: 반복 구매 허용 (제한 없음)
- `Rental`: 반복 구매 허용 (30일 재구매 제한 없음)
- `Subscription`: 스토어/서버 상태 기준으로 검증 (클라에서 임의 제한 금지)
- `SeasonPass`: 동일 `internalProductId` 중복 구매 금지 (서버에서 거부)

### 7) 구매 로컬 상태 저장은 PurchaseManager 소유 PurchaseStorage에 한정한다

- `PurchaseStorage`는 `PurchaseManager`가 소유하며 SaveData(local/cloud) 경로로 저장될 수 있다.
- 저장 범위는 **진행 중 구매 1건(current)** 의 최소 복구 상태로 제한한다.
- 구매 실패 내역/최근 실패 요약은 저장하지 않는다.
- 전체 구매 이력/영수증/토큰 저장 금지. 서버 원장(Firestore) 대체 금지.
- 정본: [33-purchase-storage](../33-purchase-storage/SKILL.md)


---


## Client API (권장 형태)


> 이 섹션은 "Devian 클라 레이어가 따라야 하는 규약"이며, 실제 코드 시그니처/클래스명은 구현 프로젝트에 맞춰 적용한다.


### 최소 기능


- Initialize: Unity IAP 초기화
- GetCatalog: 가격/통화/로컬라이즈 정보 조회(가능한 범위)
- Purchase: 구매 시작
- Restore: iOS 복원 트리거
- GetEntitlements: 서버에서 Entitlement 상태 조회(앱 시작/포그라운드 등)


### 이벤트/상태


- 구매 결과는 "스토어 결제 성공/실패"와 "서버 검증 결과"를 구분한다.
- UI/상위 로직에 반영 가능한 최종 결과는 서버 검증 결과(`GRANTED/ALREADY_GRANTED/REJECTED/PENDING`)를 따른다.


정본 규칙: [03-ssot](../03-ssot/SKILL.md)


---


## PurchaseManager (Client Entry Point)


- 샘플 구현: `com.devian.samples` — `Samples~/MobileSystem/Runtime/Purchase/PurchaseManager.cs`
- 구현: `PurchaseManager : CompoSingleton<PurchaseManager>`
- 구매 상태 스냅샷 저장: `PurchaseManager.Instance.Storage` (`PurchaseStorage`)


### 공개 메소드 규약(Policy)


- `InitializeAsync(ct)` → `Task<CommonResult>`
  - IAP 초기화 (Connect + FetchProducts). Idempotent.
- `PurchaseAsync(internalProductId, ct)` → `Task<CommonResult<PurchaseFinalResult>>`
  - 단일 구매 진입점. `TB_PRODUCT`에서 `Kind`를 조회하여 구매 유형(Consumable/Rental/Subscription/SeasonPass)을 자동 결정
  - 최종 지급은 서버 `verifyPurchase` 결과만 신뢰
  - **Caller-managed client grant**: `NeedsClientGrantDelivery=true`이면 호출자가 보상을 적용한 뒤 `AckPurchaseClientGrantAppliedAsync`로 ACK
- `RetryInterruptedPurchaseAsync(ct)` → `Task<CommonResult<RetryInterruptedPurchaseResult>>`
  - `PurchaseStorage.current`에 중단된 결제가 있으면 상태 전이를 재개/마무리
- `AckPurchaseClientGrantAppliedAsync(purchaseId, ct)` — 로컬 지급 성공 보고
- `ReportPurchaseClientGrantFailureAsync(purchaseId, ct)` — 로컬 지급 실패 보고
- `RestoreAsync(ct)` (iOS 스토어 복원, manual/fallback)
- `GetLatestConsumablePurchase30dAsync(ct)` — 최근 30일 Consumable 최신 1건 조회
- `SyncEntitlementsAsync(ct)` → `Task<CommonResult>` — 서버 entitlements 동기화 (Rental/SeasonPass → InventoryStorage)
- `RefundAsync(ct)` — 환불 상태 동기화/처리


### Hard
- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 지급량/보상 계산을 클라이언트에서 임의로 하지 않음
