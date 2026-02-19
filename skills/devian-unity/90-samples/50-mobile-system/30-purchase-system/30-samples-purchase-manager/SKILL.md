# samples-purchase-manager


PurchaseManager(구매 샘플)의 위치/역할/규약을 설명한다.


---


## Implementation Location (SSOT)


- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/PurchaseManager/PurchaseManager.cs`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/PurchaseManager/PurchaseManager.cs`


- asmdef:
  - `Devian.Samples.MobileSystem` (`Samples~/MobileSystem/Runtime/Devian.Samples.MobileSystem.asmdef`)


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
- `GetLatestRentalPurchase30dAsync(ct)` → `Task<CommonResult<RentalPurchaseItem>>`
  - 서버에서 최근 30일 내 최신 Rental 구매 1건 조회


---


## Server Integration (구현 완료)


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
- 소모성 지급량은 서버 grants/currencyDelta 결과만 신뢰한다(클라 계산 금지).
- 서버 `verifyPurchase`가 반환한 `grants[]`의 실제 적용(지급 실행)은 RewardManager(49-reward-system)에 위임한다.


---


## Known Issues


### ~~BUG-1. ConfirmPurchase 무조건 호출~~ — ✅ 수정됨

- `resultStatus` 확인 후 `GRANTED`/`ALREADY_GRANTED`만 Confirm, 나머지는 Confirm 하지 않음.


### ~~BUG-2. PENDING/REJECTED를 Success로 반환~~ — ✅ 수정됨

- `GRANTED`/`ALREADY_GRANTED`만 `CommonResult.Success`, 나머지는 `CommonResult.Failure` 반환.


### ~~ISSUE-3. ProductCatalog.LoadDefaultCatalog() 사용 (SSOT 불일치)~~ — ✅ 수정됨

- `TB_PRODUCT.GetAll()` 기반으로 교체 완료.
- `isActive` 필터링: 비활성 상품은 Unity IAP에 등록하지 않음.
- 플랫폼별 StoreSku 매핑: `#if UNITY_IOS` → `StoreSkuApple`, `#elif UNITY_ANDROID` → `StoreSkuGoogle`.
- `Kind` → `ProductType` 매핑: Consumable→Consumable, Subscription→Subscription, SeasonPass→NonConsumable, Rental→NonConsumable.


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


- `skills/devian-unity/90-samples/50-mobile-system/30-purchase-system/03-ssot/SKILL.md`
- `skills/devian-unity/90-samples/50-mobile-system/30-purchase-system/09-ssot-operations/SKILL.md`
