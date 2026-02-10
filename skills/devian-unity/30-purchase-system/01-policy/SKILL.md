# 30-purchase-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Devian의 인앱 결제 모듈(클라이언트) 설계/코딩 규약을 정의한다.


- 결제 SDK는 Unity IAP를 사용한다.
- 결제 검증/지급 결정은 **클라이언트가 아닌 서버(Cloud Functions)**가 담당한다.
- 게임 로직은 Store SKU / 스토어별 영수증 구조를 직접 알지 않는다(내부 ID만 사용).


---


## Hard Rules


### 1) 클라이언트는 "지급 결정"을 하지 않는다


- Unity IAP 콜백에서 "성공"이 와도 **즉시 지급 금지**
- 반드시 서버(Cloud Functions)의 `verifyPurchase` 결과를 기준으로 Entitlement를 반영한다.


정본 규칙: [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md)


### 2) 내부 표준 ID만 게임 로직에 노출한다


- 게임 로직은 `internalProductId`만 사용한다.
- Store SKU(googlePlayProductId / appStoreProductId)는 카탈로그/매핑 레이어로만 취급한다.


정본 규칙: [05-ssot-product-catalog](../05-ssot-product-catalog/SKILL.md)


### 3) iOS Restore 지원


- iOS는 Restore(복원) 플로우를 제공해야 한다.
- Android는 UX상 "자동 복원/동기화" 패턴을 주로 사용해도 된다.
- 단, 최종 상태는 서버의 Entitlement 기준.


정본 규칙: [04-ssot-unity-iap](../04-ssot-unity-iap/SKILL.md), [07-ssot-subscription-noads](../07-ssot-subscription-noads/SKILL.md)


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
- UI/게임 로직에 반영 가능한 최종 결과는 서버 검증 결과(`GRANTED/ALREADY_GRANTED/REJECTED/PENDING`)를 따른다.


정본 규칙: [06-ssot-verify-idempotency](../06-ssot-verify-idempotency/SKILL.md)


---


## PurchaseManager (Client Entry Point)


- 샘플 구현: `com.devian.samples` — `Samples~/IAP/Runtime/PurchaseManager.cs`
- 구현: `PurchaseManager : CompoSingleton<PurchaseManager>`


### 공개 메소드 규약(Policy)


- `PurchaseConsumableAsync(internalProductId, ct)`
  - 예: 보물상자/재화팩
  - 최종 지급(grants/currencyDelta)은 서버 `verifyPurchase` 결과만 신뢰


- `PurchaseSubscriptionAsync(internalProductId, ct)`
  - 예: NoAds 구독
  - NoAds 판정은 "현재 Active 상태(서버)" 기준 (클라 콜백만으로 영구 적용 금지)


- `PurchaseSeasonPassAsync(internalProductId, ct)`
  - 시즌별 1회 구매(Subscription 아님)


- `RestoreAsync(ct)` (iOS)
- `SyncEntitlementsAsync(ct)` / `getEntitlements`


### Hard
- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 지급량/보상 계산을 클라이언트에서 임의로 하지 않음
