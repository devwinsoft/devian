---
name: 15-shop-product-factory
description: "`ShopProductFactory`로 테이블 row -> `ShopProductBase` 변환/dispatch 경로를 표준화할 때 사용한다."
---

# 15-shop-product-factory

Status: ACTIVE
AppliesTo: v10
Type: Design / Factory SSOT

## Purpose

`ShopProductFactory`는 테이블 row -> `ShopProductBase` 변환을 표준화한다.

- `CURRENCY_TYPE` -> 구현 클래스(`Free/Ads/Currency`) dispatch를 한 곳에서 수행한다.
- 카탈로그 타입별 row -> product 변환 helper를 제공한다.

---

## Factory Shape

```csharp
internal static class ShopProductFactory
{
    public static ShopProductBase CreateDailyProduct(SHOP_DAILY row, SHOP_DISCOUNT_TYPE discountType);
    public static ShopProductBase CreateChestProduct(SHOP_CHEST row);
    public static ShopProductBase CreateEventProduct(SHOP_EVENT row);
    public static ShopProductBase CreatePurchaseProduct(SHOP_PURCHASE row);
    public static ShopProductBase CreateGoldProduct(SHOP_GOLD row);
    public static IReadOnlyList<ShopProductBase> BuildProductsFromRows<TRow>(
        IReadOnlyList<TRow> rows, Func<TRow, ShopProductBase> createProduct);
}
```

---

## Hard Rules

- `CURRENCY_TYPE` -> 구현 클래스(`ShopProductFree/ShopProductAds/ShopProductCurrency`) dispatch는 이 파일에서만 수행한다.
- `PURCHASE` 상품은 reward 경로를 거치지 않고 `ShopProductPurchase`를 직접 생성한다.
- 비즈니스 로직(선택/할인/시간 필터)은 factory가 아닌 catalog 계층에 둔다.
- Factory는 storage 접근/상태 저장 책임을 가지지 않는다.
- `BuildProductsFromRows`는 null row를 skip하고 createProduct 결과가 null이면 skip한다.

---

## Product Type Routing

| Currency_type | Product Class | ProductType |
|-------------|---------------|------------|
| `FREE` | `ShopProductFree` | `FREE` |
| `ADS` | `ShopProductAds` | `ADS` |
| 그 외 | `ShopProductCurrency` | `CURRENCY` |
| (PURCHASE row) | `ShopProductPurchase` | `PURCHASE` |

---

## Row -> Product Mapping

| Method | Source Table | 특이사항 |
|--------|------------|----------|
| `CreateDailyProduct` | `SHOP_DAILY` | `discountType` 파라미터 |
| `CreateChestProduct` | `SHOP_CHEST` | `NONE` discount |
| `CreateEventProduct` | `SHOP_EVENT` | `amount=1`, `max_count=-1` 고정 |
| `CreatePurchaseProduct` | `SHOP_PURCHASE` | reward 경로 아님, `max_count=-1` 고정 |
| `CreateGoldProduct` | `SHOP_GOLD` | `amount=1`, `NONE` discount |

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/ShopProductFactory.cs`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
