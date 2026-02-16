# purchase-store-apple-implementation

`Devian.IPurchaseStore` (PurchaseManager 샘플이 소유하는 public interface, Unity IAP 5.x v5 기준)의 Apple 구현 문서.

## Implementation Location

- UPM: `framework-cs/upm/com.devian.purchase.store.apple/Runtime/PurchaseStoreApple.cs`
- UnityExample: `framework-cs/apps/UnityExample/Packages/com.devian.purchase.store.apple/Runtime/PurchaseStoreApple.cs`

## Responsibilities (IPurchaseStore v5)

- `StoreKey`는 `"apple"`.
- `BuildVerifyPayload(string receipt)`는 입력 `receipt`를 그대로 반환한다.

## Notes

- Restore/스토어 트랜잭션 재동기화는 `IPurchaseStore` 책임이 아니다. (PurchaseManager가 StoreController v5 API로 처리)
- 정본: [04-ssot-unity-iap](../04-ssot-unity-iap/SKILL.md)
