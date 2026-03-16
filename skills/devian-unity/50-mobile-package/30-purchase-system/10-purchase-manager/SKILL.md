# 10-purchase-manager

Status: ACTIVE
AppliesTo: v13

PurchaseManager(구매 샘플)의 위치/역할/규약을 설명한다.

## 문서 경계 (Scope)

- 이 문서는 **PurchaseManager 클라이언트 샘플 코드의 위치/흐름/규약**을 설명한다.
- Firebase Functions 구현 상세, Firestore 스키마, 배포/레포 셋업을 이 문서에 복제하지 않는다.
- 서버 관련 정본은 `40`(구현), `44`(셋업), `46`(결정), `43`(클라-서버 연동 규약)를 참조한다.

PurchaseManager는 **단일 concrete 클래스**이다.
`TB_PURCHASE` 테이블을 직접 참조하여 `internalProductId -> rewardGroupId` 변환과 ProductDefinition 빌드를 수행한다.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/04-package-policy](../../../../devian-unity/04-package-policy/SKILL.md), [devian-unity/01-policy](../../../../devian-unity/01-policy/SKILL.md) §SSOT 원칙

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseManager.cs`
- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseSettings.cs`
- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseStorage.cs` (상태 스냅샷)
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseSettings.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Purchase/PurchaseStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Purchase/PurchaseManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Purchase/PurchaseSettings.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Purchase/PurchaseStorage.cs`


- asmdef:
  - `Devian.Samples.MobilePackage` (`Samples~/MobilePackage/Runtime/Devian.Samples.MobilePackage.asmdef`)
  - 참조: `Devian.Domain.Game` (TB_PURCHASE 테이블), `Devian.Domain.Common` (CommonResult)


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
- `PurchaseAsync(internalProductId, ct)` → `Task<CommonResult<PurchaseFinalResult>>`
  - 단일 구매 진입점. `TB_PURCHASE`에서 `Kind`를 조회하여 구매 유형(Consumable/Rental/Subscription/SeasonPass)을 자동 결정
  - 최종 지급은 서버 `verifyPurchase` 결과만 신뢰
  - 구매 보상(`AppliedRewards`)에 `amount < 0` 값이 있으면 즉시 실패(`COMMON_INVALID_ARGUMENT`) 처리한다.
  - **Caller-managed client grant**: `NeedsClientGrantDelivery=true`이면 호출자가 보상을 적용한 뒤 `AckPurchaseClientGrantAppliedAsync`로 ACK
- `RetryInterruptedPurchaseAsync(ct)` → `Task<CommonResult<RetryInterruptedPurchaseResult>>`
  - `PurchaseStorage.current`에 중단된 결제가 있으면 새 구매를 시작하지 않고 상태 전이를 재개/마무리
  - 중단 내역이 없으면 `Success(Status=SkippedNoCurrent)` (skip, 정상)
  - UI/Debug 표시는 반환 payload(`Status`, `InternalProductId`, `ResultStatus`, `AppliedRewards`)를 사용
- `AckPurchaseClientGrantAppliedAsync(purchaseId, ct)` — 로컬 지급 성공 보고
- `ReportPurchaseClientGrantFailureAsync(purchaseId, ct)` — 로컬 지급 실패 보고
- `RestoreAsync(ct)` (iOS 스토어 복원, manual/fallback)
  - 스토어 복원 직후 서버 `getEntitlements`를 조회해 `mPasses/mRentals`를 덮어써 복원한다.
  - `pass`는 시즌 유효 구간(`TB_SEASON`)일 때만 복원한다.
  - `rental`은 만료시각이 서버 현재시각보다 미래일 때만 복원하며, 누적 연장을 허용한다.
- `GetLatestConsumablePurchase30dAsync(ct)` → `Task<CommonResult<RecentPurchaseItem>>`
  - 서버에서 최근 30일 내 최신 Consumable 구매 1건 조회
- `SyncEntitlementsAsync(ct)` → `Task<CommonResult>`
  - 서버 `getEntitlements`를 조회해 `mPasses/mRentals`를 덮어써 동기화하고 결과 스냅샷을 반환한다.
  - `rental`은 만료시각 미래값만 반영하며, 누적 연장을 허용한다.
- `RefundAsync(ct)` → `Task<CommonResult<RefundResult>>`
  - 서버에서 환불/조정 내역을 조회하여 로컬 인벤토리 회수 적용


---


## 컨텐츠 카탈로그 통합 (TB_PURCHASE 직접 참조)

PurchaseManager가 Game 도메인 테이블을 직접 참조한다:

- `ResolveRewardGroupId(internalProductId)`: `TB_PURCHASE`에서 `RewardGroupId` 조회 (없으면 빈 값)
- `PurchaseFinalResult`, `RetryInterruptedPurchaseResult`
  - `RewardGroupId`와 `AppliedRewards: RewardData[]`를 포함한다.
  - `skip`/`rewardGroupId 없음`/이번 호출에서 지급 없음(`ALREADY_GRANTED + APPLIED_ACKED`)은 `AppliedRewards=[]`가 정상이다.
- `BuildProductDefinitions()`: `TB_PURCHASE.GetAll()`에서 `isActive` 필터링 후 ProductDefinition 목록 생성
  - 플랫폼별 StoreSku 매핑: `#if UNITY_IOS` → `StoreSkuApple`, `#elif UNITY_ANDROID` → `StoreSkuGoogle`
  - `Kind` → `ProductType` 매핑: Consumable→Consumable, Subscription→Subscription, SeasonPass→NonConsumable


---


## Server Integration (구현 완료)

서버 구현의 상세 규칙/스키마/배포 절차는 이 문서가 아니라 `40/44/46` 문서를 우선한다.


- **VerifyPurchaseAsync**: `FirebaseCallableManager.Instance.VerifyPurchaseAsync(data, ct)` — ✅ 구현됨
  - 요청 키: `storeKey`, `internalProductId`, `kind`, `payload`
  - 응답: `CommonResult<VerifyPurchaseResponse>`
- **reportPurchaseClientGrantResultAsync**: `FirebaseCallableManager.Instance.AckPurchaseClientGrantAsync(data, ct)` — ✅ 구현됨
  - 요청 키: `purchaseId`, `clientGrantStatus`
  - 응답: `CommonResult`
- **ackPurchaseStoreConfirmAsync**: `FirebaseCallableManager.Instance.AckPurchaseStoreConfirmAsync(data, ct)` — ✅ 구현됨
  - 요청 키: `purchaseId`
  - 응답: `CommonResult`
- **SyncEntitlementsAsync**: `FirebaseCallableManager.Instance.GetEntitlementsAsync(ct)` 기반 복원 — ✅ 구현됨
  - 응답: `CommonResult<EntitlementsSnapshot>` (서버 entitlements를 `mPasses/mRentals`에 덮어쓴 결과)
- **GetLatestConsumablePurchase30dAsync**: `FirebaseCallableManager.Instance.GetRecentPurchases30dAsync(data, ct)` — ✅ 구현됨
  - 응답: `CommonResult<RecentPurchaseItem>`
- **syncRefundsPageAsync**: `FirebaseCallableManager.Instance.GetPurchaseAdjustmentsAsync(data, ct)` — ✅ 구현됨
  - 응답: `CommonResult<RefundPageResult>` (raw item → PurchaseManager가 domain 보강)
- **ackRefundAppliedAsync**: `FirebaseCallableManager.Instance.AckRefundAppliedAsync(data, ct)` — ✅ 구현됨
  - 응답: `CommonResult`
- Firebase callable 호출/에러 매핑/응답 파싱은 [23-firebase-callable-manager](../../23-firebase-callable-manager/SKILL.md)에 통합.
  PurchaseManager는 `FunctionsException`을 직접 catch하지 않는다. `using Firebase.Functions` 불필요.
  domain 변환(ResolveRewardGroupId 등)은 PurchaseManager가 typed result 수신 후 수행한다.


---


## Post-Sync Orchestration (SaveData 로드 후 표준 순서)

1. `SaveDataManager.SyncGameStorageAsync(ct)` → `SyncResult`
2. `SaveDataManager`가 성공 시 payload를 직접 복원 → inventory, purchase, account 역직렬화
3. `PurchaseManager.InitializeAsync(ct)` → IAP 초기화
4. `PurchaseManager.RetryInterruptedPurchaseAsync(ct)` → 중단 구매 복구
5. `PurchaseManager.RefundAsync(ct)` → 환불 처리
6. `PurchaseManager.SyncEntitlementsAsync(ct)` → 서버 `getEntitlements` 기준으로 `mPasses/mRentals` 덮어쓰기 복원
   - `pass`: 시즌 유효 구간 체크
   - `rental`: 만료시각 미래값만 반영, 누적 연장 허용


---


## Hard Rules (Sample must follow)


- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 최종 지급/상태 반영은 서버(Cloud Functions) 결과(verifyPurchase)만 기준으로 한다.
- 지급 여부는 서버 `verifyPurchase.resultStatus`만 기준으로 한다(스토어 콜백만으로 지급 금지).
- `resultStatus == GRANTED`이면 `ConfirmPurchase` + `ackPurchaseStoreConfirm`를 먼저 수행한 뒤 로컬 지급/`ackPurchaseClientGrant(APPLIED_ACKED)`를 진행한다.
- `PurchaseStorage.current`는 `Confirm + storeConfirm ACK + clientGrant report`가 종결되기 전에는 clear하지 않는다.
- 구매 보상(`RewardData[]`)의 `amount < 0`은 허용하지 않는다. 음수 보상이 감지되면 구매/복구 경로를 실패 처리한다.
- 로컬 지급 실패 시 `FAILED_REPORTED`를 서버에 기록할 수 있으나, confirm 미완 상태에서는 `current`를 유지해 복구 경로를 보존한다.
- `resultStatus == ALREADY_GRANTED`라도 `clientGrantStatus == PENDING` 또는 `FAILED_REPORTED`이면 로컬 지급 복구 경로를 사용할 수 있다.
- `rewardGroupId`가 비어 있으면 로컬 보상 지급은 스킵하고, 클라이언트 지급 완료 처리만 진행한다. (결제/재구매 동일 규칙)
- 구매 전 인증 게이트는 `LoginManager` API(`IsPurchaseLoginReady`, `EnsurePurchaseLoginReadyAsync`)를 사용한다. (`FirebaseAuth.CurrentUser` 직접 판정 금지)
- 로컬/클라우드 저장용 구매 상태는 `PurchaseManager.Instance.Storage`(`PurchaseStorage`)에 기록한다.
- `PurchaseStorage`는 진행 중 결제(`current`), 환불/지원 대응용 최소 로그(`refundSupportLogs`), 환불 동기화 상태(`refundSync`)를 저장한다.
- 실패 코드/메시지/최근 실패 요약/전체 이력/영수증(raw receipt)은 저장 금지.
- 환불/지원 로그 조회/삭제는 `PurchaseStorage` API를 직접 사용한다.
  - `GetRefundSupportLogs()`, `TryGetRefundSupportLog(...)`, `RemoveRefundSupportLog(purchaseId)`, `ClearRefundSupportLogs()`


---


## Related SSOT


- `skills/devian-unity/50-mobile-package/30-purchase-system/14-purchase-settings/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/03-ssot/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/33-purchase-storage/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/09-ssot-operations/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/40-purchase-backend-firebase/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/43-purchase-client-server-integration/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/11-purchase-repo-firebase-functions-setup/SKILL.md`
- `skills/devian-unity/50-mobile-package/30-purchase-system/46-purchase-decisions/SKILL.md`
