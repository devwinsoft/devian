---
name: 15-shop-product-factory
description: "`ShopProductFactory`로 `SHOP_ITEM_*` row -> table-centric `ShopProductBase` 변환 경로를 표준화할 때 사용한다."
---

# 15-shop-product-factory

Status: ACTIVE
AppliesTo: v10
Type: Design / Factory SSOT

## Purpose

`ShopProductFactory`는 테이블 row -> `ShopProductBase` 변환을 표준화한다.

- source table -> concrete product class dispatch를 한 곳에서 수행한다.
- 카탈로그 타입별 row -> product 변환 helper를 제공한다.

---

## Factory Shape

```csharp
internal static class ShopProductFactory
{
    public static ShopProductBase CreateDailyProduct(SHOP_ITEM_DAILY row, SHOP_DISCOUNT_TYPE discountType);
    public static ShopProductBase CreateChestProduct(SHOP_ITEM_CHEST row);
    public static ShopProductBase CreateEventProduct(SHOP_ITEM_EVENT row);
    public static ShopProductBase CreatePurchaseProduct(SHOP_ITEM_PURCHASE row);
    public static ShopProductBase CreateGoldProduct(SHOP_ITEM_GOLD row);
    public static IReadOnlyList<ShopProductBase> BuildProductsFromRows<TRow>(
        IReadOnlyList<TRow> rows, Func<TRow, ShopProductBase> createProduct);
}
```

---

## Hard Rules

- concrete product class 선택은 source table 기준으로만 수행한다.
- `FREE/ADS/CURRENCY` 차이는 구현 클래스가 아니라 product 내부의 `ProductType`/`currency_type` 해석으로 처리한다.
- `PURCHASE` 상품은 reward 경로를 거치지 않고 `ShopProductPurchase`를 직접 생성한다.
- 비즈니스 로직(선택/할인/시간 필터)은 factory가 아닌 catalog 계층에 둔다.
- Factory는 storage 접근/상태 저장 책임을 가지지 않는다.
- `BuildProductsFromRows`는 null row를 skip하고 createProduct 결과가 null이면 skip한다.

---

## Product Routing

| Source Table | Product Class | ProductType 결정 |
|-------------|---------------|-------------------|
| `SHOP_ITEM_DAILY` | `ShopProductDaily` | `row.currency_type` |
| `SHOP_ITEM_EVENT` | `ShopProductEvent` | `row.currency_type` |
| `SHOP_ITEM_GOLD` | `ShopProductGold` | `row.currency_type` |
| `SHOP_ITEM_CHEST` | `ShopProductChest` | `row.chest_type` + `row.currency_type` |
| `SHOP_ITEM_PURCHASE` | `ShopProductPurchase` | `PURCHASE` 고정 |

---

## Row -> Product Mapping

| Method | Source Table | 특이사항 |
|--------|------------|----------|
| `CreateDailyProduct` | `SHOP_ITEM_DAILY` | `discountType` + snapshot `amount` 파라미터, `SHOP_ITEM_DAILY.unit_price`는 단가이며 최종 기본 가격은 product에서 `unit_price * amount`로 계산 |
| `CreateChestProduct` | `SHOP_ITEM_CHEST` | `NONE` discount |
| `CreateEventProduct` | `SHOP_ITEM_EVENT` | `amount=1`, `max_count=-1` 고정 |
| `CreatePurchaseProduct` | `SHOP_ITEM_PURCHASE` | reward 경로 아님, `max_count=-1` 고정 |
| `CreateGoldProduct` | `SHOP_ITEM_GOLD` | `amount=1`, `NONE` discount |

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/ShopProductFactory.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/Shop/ShopProductFactory.cs`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
