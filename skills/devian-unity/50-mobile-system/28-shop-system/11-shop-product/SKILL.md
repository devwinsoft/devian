---
name: 11-shop-product
description: ShopTable row를 `ShopProductBase` 계층(`Free/Ads/Currency/Purchase/Chest`)으로 변환하고 `maxCount/remainCount`, 할인 반영 `Price`를 모델링할 때 사용한다.
---

# 11-shop-product

Status: ACTIVE
AppliesTo: v10

ShopProduct 모델은 카탈로그 row를 `ShopProductBase` 계층으로 분류한다.

---

## 1. Source Tables

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_DAILY`
- `SHOP_CHEST`
- `SHOP_PURCHASE`
- `SHOP_GOLD`

기존 `SHOP_PRODUCT`/`productId` 개념은 제거하고 `shopId`를 사용한다.

---

## 2. Class Contract

```csharp
public abstract class ShopProductBase
{
    public string ShopId { get; }
    public SHOP_CATALOG_TYPE CatalogType { get; }
    public SHOP_PRODUCT_TYPE ProductType { get; }
    public SHOP_DISCOUNT_TYPE DiscountType { get; }
    public int PriceWithoutDiscount { get; }
    public int Price { get; } // DiscountType 반영 최종 가격
    public string NameId { get; }
    public int MaxCount { get; }
    public int RemainCount { get; }
}

public abstract class ShopRewardProductBase : ShopProductBase
{
    public CURRENCY_TYPE CurrencyType { get; }
    public string RewardGroupId { get; }
    public int Amount { get; }
}

public sealed class ShopProductFree : ShopRewardProductBase {}
public sealed class ShopProductAds : ShopRewardProductBase {}
public sealed class ShopProductCurrency : ShopRewardProductBase {}
public sealed class ShopProductChest : ShopProductBase
{
    public SHOP_PRODUCT_CHEST_TYPE ChestType { get; }
    public CURRENCY_TYPE CurrencyType { get; }
    public int Amount { get; }
}
public sealed class ShopProductPurchase : ShopProductBase
{
    public string InternalProductId { get; }
    public string SeasonId { get; }
}
```

- 구매 방식 분류:
- `FREE` -> `ShopProductFree`
- `ADS` -> `ShopProductAds`
- `CURRENCY` -> `ShopProductCurrency`
- `CHEST` -> `ShopProductChest`
- `PURCHASE` -> `ShopProductPurchase`
- `SHOP_GOLD`는 `amount`가 없으므로 `Amount=1` 고정이다.
- `SHOP_CHEST.amount`는 reward 반복 지급 횟수다.

---

## 3. Hard Rules

- `shopId`가 유일 키다.
- `SHOP_PRODUCT_TYPE.NONE`은 placeholder 용도이며 실제 판매 상품에는 사용하지 않는다.
- `PURCHASE` 타입은 `InternalProductId`가 필수다.
- `DiscountType`은 `SHOP_DAILY` 초기화/refresh 시에만 랜덤 배정되며, 나머지 카탈로그는 `SHOP_DISCOUNT_TYPE.NONE`을 사용한다.
- `ShopManager.GetProducts(...)` API는 사용하지 않고, `GetCatalog(catalogType).GetProducts()`/`GetCatalog(catalogType).GetProduct(shopId)`를 사용한다.
- `MaxCount` 기본값은 `-1`(무제한)이며, 제한 상품은 `RemainCount`를 통해 현재 남은 구매 가능 횟수를 관리한다.
- `ShopProductBase` 생성 시 기본 `RemainCount = MaxCount`로 초기화한다.
- `ShopProductBase.Price`는 `SHOP_DISCOUNT_TYPE`을 적용한 최종 가격이다.
- 원가(테이블 가격)는 `ShopProductBase.PriceWithoutDiscount`로 보존한다.
- `SHOP_CHEST`는 `rewardGroupId`를 직접 가지지 않는다. chest 구매 reward는 현재 `ShopCatalogChest.Level`의 `SHOP_CATALOG_CHEST.rewardAds/rewardPaid01/rewardPaid10`에서 동적으로 결정한다.
- `ShopProductChest.ProductType`은 `ChestType=ADS`면 `SHOP_PRODUCT_TYPE.ADS`, `ChestType=ONE/TEN`이면 `SHOP_PRODUCT_TYPE.CURRENCY`로 해석한다.
- 카탈로그 클래스(`ShopCatalogBase`, `ShopCatalogDaily/Chest/Purchase/Gold`)는 `ShopCatalog.cs`에 분리되어 관리한다.
- row -> `ShopProductBase` 변환은 `ShopProductFactory`(15)에서 처리한다.

---

## 4. Implementation Location (3-path mirror)

클래스별 파일 분리 — `Product/` 폴더 (8개 파일):
- `ShopProductBase.cs`, `ShopRewardProductBase.cs`, `ShopProductNone.cs`
- `ShopProductFree.cs`, `ShopProductAds.cs`, `ShopProductCurrency.cs`
- `ShopProductChest.cs`, `ShopProductPurchase.cs`

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/Product/` (8개 파일)
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/Product/` (8개 파일)
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/Product/` (8개 파일)

---

## 5. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [15-shop-product-factory](../15-shop-product-factory/SKILL.md)
