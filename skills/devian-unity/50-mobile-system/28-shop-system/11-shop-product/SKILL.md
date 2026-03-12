---
name: 11-shop-product
description: ShopTable의 `SHOP_DAILY/CHEST/PURCHASE/GOLD`를 구매 방식 enum(`SHOP_PRODUCT_TYPE`) 기반의 `ShopProductBase` 계층(`Free/Ads/Currency/Purchase`)으로 구성할 때 사용한다.
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
    public string NameId { get; }
    public int MaxCount { get; }
}

public abstract class ShopRewardProductBase : ShopProductBase
{
    public CURRENCY_TYPE CurrencyType { get; }
    public int Price { get; }
    public string RewardGroupId { get; }
    public int Amount { get; }
}

public sealed class ShopProductFree : ShopRewardProductBase {}
public sealed class ShopProductAds : ShopRewardProductBase {}
public sealed class ShopProductCurrency : ShopRewardProductBase {}
public sealed class ShopProductPurchase : ShopProductBase
{
    public string InternalProductId { get; }
    public string SeasonId { get; }
}

public sealed class ShopCatalog
{
    public SHOP_CATALOG_TYPE CatalogType { get; }
    public IReadOnlyList<ShopProductBase> Products { get; }
}
```

- 구매 방식 분류:
- `FREE` -> `ShopProductFree`
- `ADS` -> `ShopProductAds`
- `CURRENCY` -> `ShopProductCurrency`
- `PURCHASE` -> `ShopProductPurchase`
- `SHOP_GOLD`는 `amount`가 없으므로 `Amount=1` 고정이다.

---

## 3. Hard Rules

- `shopId`가 유일 키다.
- `SHOP_PRODUCT_TYPE.NONE`은 placeholder 용도이며 실제 판매 상품에는 사용하지 않는다.
- `PURCHASE` 타입은 `InternalProductId`가 필수다.

---

## 4. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopProduct.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopProduct.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopProduct.cs`

---

## 5. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
