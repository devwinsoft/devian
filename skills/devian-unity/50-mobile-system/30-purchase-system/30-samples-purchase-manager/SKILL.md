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
  - IAP 초기화 (Connect + FetchProducts). 명시적 호출 필수.
  - Idempotent: 여러 번 호출해도 동일 Task 반환.
  - Editor에서는 즉시 `PURCHASE_UNSUPPORTED_PLATFORM` 반환.
- `PurchaseConsumableAsync(internalProductId, ct)`
  - 예: 보물상자(소모성)
- `PurchaseSubscriptionAsync(internalProductId, ct)`
  - 예: NoAds 구독
- `PurchaseSeasonPassAsync(internalProductId, ct)`
  - 시즌별 1회 구매(Subscription 아님)
- `RestoreAsync(ct)` (iOS 복원)
- `SyncEntitlementsAsync(ct)` (서버 상태 동기화)
- `GetLatestConsumablePurchase30dAsync(ct)` → `Task<CommonResult<RecentPurchaseItem>>`
  - 서버에서 최근 30일 내 최신 Consumable 구매 1건 조회


---


## 컨텐츠 카탈로그 통합 (TB_PRODUCT 직접 참조)

PurchaseManager가 Game 도메인 테이블을 직접 참조한다:

- `ResolveRewardGroupId(internalProductId)`: `TB_PRODUCT.Get(internalProductId).RewardGroupId` — 테이블 조회로 변환
- `BuildProductDefinitions()`: `TB_PRODUCT.GetAll()`에서 `isActive` 필터링 후 ProductDefinition 목록 생성
  - 플랫폼별 StoreSku 매핑: `#if UNITY_IOS` → `StoreSkuApple`, `#elif UNITY_ANDROID` → `StoreSkuGoogle`
  - `Kind` → `ProductType` 매핑: Consumable→Consumable, Subscription→Subscription, SeasonPass→NonConsumable


---


## Server Integration (구현 완료)

서버 구현의 상세 규칙/스키마/배포 절차는 이 문서가 아니라 `40/44/46` 문서를 우선한다.


- **VerifyPurchaseAsync**: Firebase Functions Callable (`verifyPurchase`) 사용 — ✅ 구현됨
  - 요청 키: `storeKey`, `internalProductId`, `kind`, `payload`
  - 응답 키: `resultStatus`, `grants`, `entitlementsSnapshot`
- **SyncEntitlementsAsync**: Firebase Functions Callable (`getEntitlements`) 사용 — ✅ 구현됨
  - `uid`는 Firebase Auth context에서 자동 전달
- SDK: `Firebase.Functions` (Firebase Unity SDK 13.7.0)
- asmdef: `overrideReferences: false` → Plugins의 `Firebase.Functions.dll` 자동 참조 (명시 추가 불필요)


---


## Hard Rules (Sample must follow)


- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 최종 지급/상태 반영은 서버(Cloud Functions) 결과(verifyPurchase/getEntitlements)만 기준으로 한다.
- 지급 여부는 서버 `verifyPurchase.resultStatus`만 기준으로 한다(스토어 콜백만으로 지급 금지).
- `resultStatus == GRANTED`일 때만 `ResolveRewardGroupId(internalProductId)` → `rewardGroupId` 변환 후 `Singleton.Get<RewardManager>().ApplyRewardGroupId(rewardGroupId)`로 지급 실행을 위임한다.
- 구매 전 인증 게이트는 AccountManager 로그인 상태 API를 사용한다. (`FirebaseAuth.CurrentUser` 직접 판정 금지)
- 로컬/클라우드 저장용 구매 상태는 `PurchaseManager`가 직접 소유하지 않고 `GameStorageManager.Instance.Purchase`(`PurchaseStorage`)에 기록한다.
- `PurchaseStorage`는 최소 스냅샷만 저장한다(진행 중 1건 + 최근 결과 1건). 전체 이력/영수증 저장 금지.


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
