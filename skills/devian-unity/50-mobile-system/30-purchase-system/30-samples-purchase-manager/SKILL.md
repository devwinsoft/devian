# samples-purchase-manager


PurchaseManager(구매 샘플)의 위치/역할/규약을 설명한다.

## 문서 경계 (Scope)

- 이 문서는 **PurchaseManager 클라이언트 샘플 코드의 위치/흐름/규약**을 설명한다.
- Firebase Functions 구현 상세, Firestore 스키마, 배포/레포 셋업을 이 문서에 복제하지 않는다.
- 서버 관련 정본은 `40`(구현), `44`(셋업), `46`(결정), `43`(클라-서버 연동 규약)를 참조한다.

PurchaseManager는 **단일 concrete 클래스**이다.
`TB_PRODUCT` 테이블을 직접 참조하여 `internalProductId -> rewardGroupId` 변환과 ProductDefinition 빌드를 수행한다.


---


## Implementation Location (SSOT)


- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseManager.cs`
- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs` (상태 스냅샷)
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseManager.cs`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Purchase/PurchaseManager.cs`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Purchase/PurchaseStorage.cs`


- asmdef:
  - `Devian.Samples.MobileSystem` (`Samples~/MobileSystem/Runtime/Devian.Samples.MobileSystem.asmdef`)
  - 참조: `Devian.Domain.Game` (TB_PRODUCT 테이블), `Devian.Domain.Common` (CommonResult)


---


## Singleton

```csharp
CompoSingleton<PurchaseManager>.Instance
```

- Registry key: `PurchaseManager`
- 다른 매니저에서 접근: `Singleton.Get<PurchaseManager>()`


---


## Public API (Sample)


- `InitializeAsync(ct)` → `Task<CommonResult>`
  - IAP 초기화 (Connect + FetchProducts). 선택적 prewarm 호출이며, Purchase/RetryInterruptedPurchase/Restore 경로에서 lazy-init로 자동 호출될 수 있음.
  - Idempotent: 여러 번 호출해도 동일 Task 반환.
  - Editor에서는 즉시 `PURCHASE_UNSUPPORTED_PLATFORM` 반환.
- `PurchaseConsumableAsync(internalProductId, ct)`
  - 예: 보물상자(소모성)
- `PurchaseSubscriptionAsync(internalProductId, ct)`
  - 예: NoAds 구독
- `PurchaseSeasonPassAsync(internalProductId, ct)`
  - 시즌별 1회 구매(Subscription 아님)
- `RetryInterruptedPurchaseAsync(ct)` → `Task<CommonResult<RetryInterruptedPurchaseResult>>`
  - `PurchaseStorage.current`에 중단된 결제가 있으면 새 구매를 시작하지 않고 상태 전이를 재개/마무리
  - 중단 내역이 없으면 `Success(Status=SkippedNoCurrent)` (skip, 정상)
  - UI/Debug 표시는 반환 payload(`Status`, `InternalProductId`, `ResultStatus`, `AppliedRewards`)를 사용
- `RestoreAsync(ct)` (iOS 스토어 복원, manual/fallback)
  - 일반적인 SeasonPass/Rental 복원 정본 경로는 서버 상태 동기화(`SyncEntitlementsAsync` + 향후 restore projection)이며, `RestoreAsync()`와 개념을 분리한다.
- `SyncEntitlementsAsync(ct)` (서버 상태 동기화)
  - `PurchaseStorage` local/cloud 캐시 중 `seasonPassOwnership`를 갱신한다.
- `GetRentalRemainingMsAsync(internalProductId, ct)` → `Task<CommonResult<long>>`
  - 서버(`getEntitlements`)의 `rentals` projection을 질의하여 남은 시간(ms)을 반환
  - 성공 시 `PurchaseStorage.noAdsExpireAtClientUtcMs`를 클라이언트 시간 기준으로 갱신한다.
  - 게임 로직은 `PurchaseStorage.GetNoAdsExpireAtClientUtcMs()` / `PurchaseStorage.IsNoAds()`로 만기 시각/활성 여부를 판단한다.
  - rental 정보 없음이면 `Success(0)`
- `GetLatestConsumablePurchase30dAsync(ct)` → `Task<CommonResult<RecentPurchaseItem>>`
  - 서버에서 최근 30일 내 최신 Consumable 구매 1건 조회


---


## 컨텐츠 카탈로그 통합 (TB_PRODUCT 직접 참조)

PurchaseManager가 Game 도메인 테이블을 직접 참조한다:

- `ResolveRewardGroupId(internalProductId)`: `TB_PRODUCT`에서 `RewardGroupId` 조회 (없으면 빈 값)
- `PurchaseFinalResult`, `RetryInterruptedPurchaseResult`
  - `RewardGroupId`와 `AppliedRewards: RewardData[]`를 포함한다.
  - `skip`/`rewardGroupId 없음`/이번 호출에서 지급 없음(`ALREADY_GRANTED + APPLIED_ACKED`)은 `AppliedRewards=[]`가 정상이다.
- `BuildProductDefinitions()`: `TB_PRODUCT.GetAll()`에서 `isActive` 필터링 후 ProductDefinition 목록 생성
  - 플랫폼별 StoreSku 매핑: `#if UNITY_IOS` → `StoreSkuApple`, `#elif UNITY_ANDROID` → `StoreSkuGoogle`
  - `Kind` → `ProductType` 매핑: Consumable→Consumable, Subscription→Subscription, SeasonPass→NonConsumable


---


## Server Integration (구현 완료)

서버 구현의 상세 규칙/스키마/배포 절차는 이 문서가 아니라 `40/44/46` 문서를 우선한다.


- **VerifyPurchaseAsync**: Firebase Functions Callable (`verifyPurchase`) 사용 — ✅ 구현됨
  - 요청 키: `storeKey`, `internalProductId`, `kind`, `payload`
  - 응답 키: `resultStatus`, `purchaseId`, `verifyStatus`, `clientGrantStatus`, `storeConfirmStatus`, `grants`, `entitlementsSnapshot`
- **reportPurchaseClientGrantResultAsync**: Firebase Functions Callable (`ackPurchaseClientGrant`) 사용 — ✅ 구현됨
  - 요청 키: `purchaseId`, `clientGrantStatus`
  - 용도: 로컬 지급 결과 보고(성공/실패) (`APPLIED_ACKED` / `FAILED_REPORTED`)
- **ackPurchaseStoreConfirmAsync**: Firebase Functions Callable (`ackPurchaseStoreConfirm`) 사용 — ✅ 구현됨
  - 요청 키: `purchaseId`
  - 용도: `ConfirmPurchase` 완료 후 서버 `storeConfirmStatus=CONFIRMED` 기록
- **SyncEntitlementsAsync**: Firebase Functions Callable (`getEntitlements`) 사용 — ✅ 구현됨
  - `uid`는 Firebase Auth context에서 자동 전달
- SDK: `Firebase.Functions` (Firebase Unity SDK 13.7.0)
- asmdef: `overrideReferences: false` → Plugins의 `Firebase.Functions.dll` 자동 참조 (명시 추가 불필요)


---


## Hard Rules (Sample must follow)


- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 최종 지급/상태 반영은 서버(Cloud Functions) 결과(verifyPurchase/getEntitlements)만 기준으로 한다.
- 지급 여부는 서버 `verifyPurchase.resultStatus`만 기준으로 한다(스토어 콜백만으로 지급 금지).
- `resultStatus == GRANTED`이면 `ConfirmPurchase` + `ackPurchaseStoreConfirm`를 먼저 수행한 뒤 로컬 지급/`ackPurchaseClientGrant(APPLIED_ACKED)`를 진행한다.
- `PurchaseStorage.current`는 `Confirm + storeConfirm ACK + clientGrant report`가 종결되기 전에는 clear하지 않는다.
- 로컬 지급 실패 시 `FAILED_REPORTED`를 서버에 기록할 수 있으나, confirm 미완 상태에서는 `current`를 유지해 복구 경로를 보존한다.
- `resultStatus == ALREADY_GRANTED`라도 `clientGrantStatus == PENDING` 또는 `FAILED_REPORTED`이면 로컬 지급 복구 경로를 사용할 수 있다.
- `rewardGroupId`가 비어 있으면 로컬 보상 지급은 스킵하고, 클라이언트 지급 완료 처리만 진행한다. (결제/재구매 동일 규칙)
- 구매 전 인증 게이트는 AccountManager 로그인 상태 API를 사용한다. (`FirebaseAuth.CurrentUser` 직접 판정 금지)
- 로컬/클라우드 저장용 구매 상태는 `PurchaseManager`가 직접 소유하지 않고 `GameStorageManager.Instance.Purchase`(`PurchaseStorage`)에 기록한다.
- `PurchaseStorage`는 진행 중 결제(`current`), 환불/지원 대응용 최소 로그(`refundSupportLogs`), game logic cache(`noAdsExpireAtClientUtcMs`), entitlement cache(`seasonPassOwnership`)를 저장한다.
- 실패 코드/메시지/최근 실패 요약/전체 이력/영수증(raw receipt)은 저장 금지.
- 환불/지원 UI용으로 `PurchaseManager`는 환불 로그 조회/삭제 래퍼 API를 제공한다.
  - `GetRefundSupportLogs()`, `TryGetRefundSupportLog(...)`
  - `DeleteRefundSupportLog(purchaseId)`, `ClearRefundSupportLogs()`


---


## Known Issues


### ~~BUG-1. ConfirmPurchase 무조건 호출~~ — ✅ 수정됨

- `resultStatus` 확인 후 `GRANTED`/`ALREADY_GRANTED`만 Confirm, 나머지는 Confirm 하지 않음.


### ~~BUG-2. PENDING/REJECTED를 Success로 반환~~ — ✅ 수정됨

- `GRANTED`/`ALREADY_GRANTED`만 `CommonResult.Success`, 나머지는 `CommonResult.Failure` 반환.


### ~~ISSUE-3. ProductCatalog.LoadDefaultCatalog() 사용 (SSOT 불일치)~~ — ✅ 수정됨

- `TB_PRODUCT` 기반으로 교체 완료.
- `isActive` 필터링: 비활성 상품은 Unity IAP에 등록하지 않음.
- 플랫폼별 StoreSku 매핑: `#if UNITY_IOS` → `StoreSkuApple`, `#elif UNITY_ANDROID` → `StoreSkuGoogle`.
- `Kind` → `ProductType` 매핑.


### ~~ISSUE-4. 동시 구매 요청 경쟁 조건~~ — ✅ 수정됨

- `_purchaseInProgress` 플래그로 동시 호출 방어. 메서드 전체를 try/finally로 감싸서 항상 리셋.


### ~~ISSUE-5. initializeIap()의 async void 예외 미전파~~ — ✅ 수정됨

- Awake 자동 초기화 제거, `InitializeAsync()` 명시적 호출 방식으로 전환.
- `initializeIapAsync(ct)` → `Task<CommonResult>` 반환 (async void 제거).
- FetchProducts 콜백을 TCS로 await하여 초기화 완료를 보장.
- 초기화 미완료 상태에서 API 호출 시 `PURCHASE_INIT_REQUIRED` 반환.
- 에러 분류: `PURCHASE_INIT_FAILED` (Connect 실패), `PURCHASE_PRODUCT_FETCH_FAILED` (FetchProducts 실패).


---


## Related SSOT


- `skills/devian-unity/50-mobile-system/30-purchase-system/03-ssot/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/33-purchase-storage/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/09-ssot-operations/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/40-purchase-backend-firebase/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/43-purchase-client-server-integration/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/44-purchase-repo-firebase-functions-setup/SKILL.md`
- `skills/devian-unity/50-mobile-system/30-purchase-system/46-purchase-decisions/SKILL.md`
