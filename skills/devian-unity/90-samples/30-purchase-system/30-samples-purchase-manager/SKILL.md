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


- `PurchaseConsumableAsync(internalProductId, ct)`
  - 예: 보물상자(소모성)
- `PurchaseSubscriptionAsync(internalProductId, ct)`
  - 예: NoAds 구독
- `PurchaseSeasonPassAsync(internalProductId, ct)`
  - 시즌별 1회 구매(Subscription 아님)
- `RestoreAsync(ct)` (iOS 복원)
- `SyncEntitlementsAsync(ct)` (서버 상태 동기화)


---


## Hard Rules (Sample must follow)


- Unity IAP "스토어 구매 성공 콜백"만으로 지급/NoAds 적용 금지
- 최종 지급/상태 반영은 서버(Cloud Functions) 결과(verifyPurchase/getEntitlements)만 기준으로 한다.
- 소모성 지급량은 서버 grants/currencyDelta 결과만 신뢰한다(클라 계산 금지).


---


## Related SSOT


- `skills/devian-unity/90-samples/30-purchase-system/03-ssot/SKILL.md`
- `skills/devian-unity/90-samples/30-purchase-system/09-ssot-operations/SKILL.md`
