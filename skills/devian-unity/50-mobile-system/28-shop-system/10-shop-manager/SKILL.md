---
name: 10-shop-manager
description: MobileSystem ShopManager를 CompoSingleton으로 구현하고 catalog 기반 구매(`CanBuy(shopId)`, `BuyAsync(shopId)`) 플로우(통화 차감, ADS 시청, PurchaseManager 위임, 구매 제한, amount 반복 보상, 실패 래핑)를 적용할 때 사용한다.
---

# 10-shop-manager

Status: ACTIVE
AppliesTo: v10

ShopManager는 MobileSystem 상점 구매 진입점이며, 카탈로그별 상품 목록을 관리한다.

---

## 1. Class

```csharp
public sealed class ShopManager : CompoSingleton<ShopManager>
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobileSystem`
- `MobileApplication`에서 `RequireComponent(typeof(ShopManager))`로 보장한다.

---

## 2. Public API

```csharp
public bool CanBuy(string shopId)
public Task<CommonResult<RewardData[]>> BuyAsync(string shopId, CancellationToken ct = default)
public IReadOnlyList<ShopCatalog> GetCatalogs()
public ShopCatalog GetCatalog(SHOP_CATALOG_TYPE catalogType)
public IReadOnlyList<ShopProductBase> GetProducts(SHOP_CATALOG_TYPE catalogType)
public CommonResult ResetAds(SHOP_CATALOG_TYPE catalogType)
public CommonResult<long> GetAdsResetRemainingMs(SHOP_CATALOG_TYPE catalogType)
```

핵심 플로우:

1. `shopId`로 `ShopProductBase` 조회
2. `SHOP_DAILY` 카탈로그는 초기화/리셋 시 `selectRate` 규칙으로 5개 선택 생성된다.
3. `SHOP_DAILY` 선택 생성에서 동일 `shopId`(pk)는 중복 선택하지 않는다.
4. 구매 제한 리셋 체크(서버 시간 기준): FREE/ADS + 구매 제한 상품은 카탈로그별 기준 시각 + 24시간, 비 ADS + 구매 제한 상품은 전역 1일(UTC day start) 기준
5. 구매 제한(`maxCount`) 체크
6. 카탈로그 분기: `PURCHASE`는 `PurchaseManager.PurchaseAsync(internalProductId)` 위임, 그 외는 통화 차감/광고 시청 후 `RewardManager` 지급
7. 성공 시 구매 카운트 저장 + SaveData 저장
8. FREE/ADS + 구매 제한 상품은 `GetAdsResetRemainingMs(catalogType) == 0`일 때만 성공 시점을 카탈로그 기준 시각(`startedAtUtcMs`)으로 기록
9. `GetAdsResetRemainingMs(catalogType)`으로 카탈로그별 FREE/ADS 리셋까지 남은 시간(ms)을 조회

---

## 3. Currency Rules

- `FREE`: 가격 0 구매로 사용(차감 없음)
- `ADS`: 광고 시청 성공 시 구매 성공
- `JEWEL`: `JEWEL_FREE` 우선 차감 후 부족분 `JEWEL_PAID` 차감
- `NO_ADS` 대여(`InventoryStorage.GetRentalRemainingMs("NO_ADS") > 0`) 중이면 `CanBuy`의 `AdsManager.CanShow` 체크와 `BuyAsync`의 광고 show를 skip한다.
- `ResetAds(catalogType)`: 지정 카탈로그의 ADS 구매 제한 카운트를 즉시 초기화한다.
- `GetAdsResetRemainingMs(catalogType)`: 지정 카탈로그 FREE/ADS 구매 제한 리셋까지 남은 시간(ms)을 반환한다.

---

## 4. Catalog Rules

- `DAILY`, `CHEST`, `GOLD`: `RewardManager` 지급
- `PURCHASE`: `PurchaseManager` 구매 처리 사용 (`BuyAsync` 보상 지급 로직 직접 사용 금지)
- `seasonId` 구매 제한(시즌 종료 임박 차단)은 ShopManager에서 검사한다.

---

## 5. Error Rules

- `CanBuy` 실패: `LastCanBuyErrorCode = SHOP_CAN_BUY_FAILED`
- `BuyAsync` 실패: `COMMON_ERROR_TYPE.SHOP_BUY_FAILED`
- inner 실패 코드는 메시지에 `inner=...` 형태로 포함한다.

---

## 6. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopManager.cs`

---

## 7. Related

- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [22-inventory-system/12-inventory-wallet](../../22-inventory-system/12-inventory-wallet/SKILL.md)
