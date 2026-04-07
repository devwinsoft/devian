---
name: 11-shop-product
description: ShopTable row를 table-centric `ShopProductBase` 계층(`Daily/Event/Gold/Chest/Purchase`)으로 변환하고 가격, 할인, 구매 제한 runtime 모델을 정리할 때 사용한다.
---

# 11-shop-product

Status: ACTIVE
AppliesTo: v10

ShopProduct 모델은 `SHOP_ITEM_*` row를 1:1 concrete product class로 감싼다.
row 원본은 `Table` 프로퍼티를 통해 노출하고, runtime 상태만 product에 둔다.

---

## 1. Source Tables

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_ITEM_DAILY`
- `SHOP_ITEM_EVENT`
- `SHOP_ITEM_CHEST`
- `SHOP_ITEM_PURCHASE`
- `SHOP_ITEM_GOLD`

기존 `SHOP_PRODUCT`/`productId` 개념은 제거하고 `shop_item_id`를 사용한다.

---

## 2. Class Contract

```csharp
public interface IShopTableProduct
{
    public object TableObject { get; }
}

public interface IShopTableProduct<out TRow> : IShopTableProduct
{
    public TRow Table { get; }
}

public abstract class ShopProductBase
{
    public string shop_id { get; }
    public string name_id { get; }
    public SHOP_CATALOG_TYPE catalog_type { get; }
    public SHOP_PRODUCT_TYPE ProductType { get; }
    public SHOP_DISCOUNT_TYPE DiscountType { get; }
    public virtual int PriceWithoutDiscount { get; }
    public int Price { get; } // PriceWithoutDiscount에 DiscountType 반영
}

public abstract class ShopProductBase<TRow> : ShopProductBase, IShopTableProduct<TRow>
{
    public TRow Table { get; }
}

public abstract class ShopLimitedProductBase : ShopProductBase
{
    public int max_count { get; }
    public int RemainCount { get; }
    public bool HasPurchaseLimit { get; }
}

public abstract class ShopRewardProductBase : ShopLimitedProductBase
{
    public CURRENCY_TYPE currency_type { get; }
    public string reward_group_id { get; }
    public int amount { get; }
}

public sealed class ShopProductDaily : ShopRewardProductBase<SHOP_ITEM_DAILY> {}
public sealed class ShopProductEvent : ShopRewardProductBase<SHOP_ITEM_EVENT> {}
public sealed class ShopProductGold : ShopRewardProductBase<SHOP_ITEM_GOLD> {}
public sealed class ShopProductChest : ShopLimitedProductBase<SHOP_ITEM_CHEST> {}
public sealed class ShopProductPurchase : ShopProductBase<SHOP_ITEM_PURCHASE> {}
```

- concrete class는 source table 기준으로만 나눈다.
- `FREE/ADS/CURRENCY`는 더 이상 구현 클래스 분기가 아니라 `ProductType`/`currency_type` 해석 결과다.
- `SHOP_ITEM_GOLD`와 `SHOP_ITEM_EVENT`는 `amount` column이 없으므로 `amount=1` 고정이다.
- `SHOP_ITEM_CHEST.amount`는 reward 반복 지급 횟수다.

---

## 3. Hard Rules

- `shop_item_id`가 유일 키다.
- `Table`이 row 원본 SSOT다. `name_id`, `price`, `reward_group_id`, `internal_product_id` 같은 table 값은 product에 중복 저장하지 않는다.
- runtime 상태(`DiscountType`, `RemainCount`)만 product에 둔다.
- `PURCHASE` 타입은 `Internal_product_id`가 필수다.
- `DiscountType`은 `SHOP_ITEM_DAILY` 초기화/refresh 시에만 랜덤 배정되며, 나머지 카탈로그는 `SHOP_DISCOUNT_TYPE.NONE`을 사용한다.
- `ShopManager.GetProducts(...)` API는 사용하지 않고, `GetCatalog(catalog_type).GetProducts()`/`GetCatalog(catalog_type).GetProduct(shop_item_id)`를 사용한다.
- 제한 상품은 `ShopLimitedProductBase`만 표현한다. `max_count=-1`은 무제한이다.
- `ShopLimitedProductBase` 생성 시 기본 `RemainCount = max_count`로 초기화한다.
- `ShopProductBase.Price`는 `SHOP_DISCOUNT_TYPE`을 적용한 최종 가격이다.
- `ShopProductBase.PriceWithoutDiscount`는 "할인 전 실제 결제 기준 가격"이다.
- `SHOP_ITEM_DAILY` row는 정적 `amount`를 가지지 않고 `amount_min/amount_max`를 가진다.
- `ShopRewardProductBase`의 기본 `amount`는 `1`이다. `ShopProductDaily`만 daily snapshot에서 선택된 runtime `amount`를 주입한다.
- `SHOP_ITEM_DAILY.unit_price`는 단가다. 따라서 `ShopProductDaily.PriceWithoutDiscount = Table.unit_price * amount`이며, 할인은 그 결과에 적용한다.
- `SHOP_ITEM_EVENT.price`, `SHOP_ITEM_GOLD.price`, `SHOP_ITEM_CHEST.price`는 이미 최종 row 가격으로 해석한다. 특히 chest는 `amount`가 reward 배수일 뿐 가격에는 곱해지지 않는다.
- daily runtime `amount`는 reward 반복 횟수이면서 가격 배수에도 반영된다.
- `SHOP_ITEM_CHEST.amount`는 reward 반복 지급 횟수일 뿐 가격에는 곱하지 않는다.
- `SHOP_ITEM_CHEST`는 `reward_group_id`를 직접 가지지 않는다. chest 구매 reward는 현재 `ShopCatalogChest.Level`의 `SHOP_CATALOG_CHEST.reward_ads/reward_paid01/reward_paid10`에서 동적으로 결정한다.
- `ShopProductChest.ProductType`은 `Chest_type=ADS`면 `SHOP_PRODUCT_TYPE.ADS`, `Chest_type=ONE/TEN`이면 `SHOP_PRODUCT_TYPE.CURRENCY`로 해석한다.
- `ShopProductDaily/Event/Gold`의 `ProductType`은 각 row의 `currency_type`에서 계산한다.
- 카탈로그 클래스(`ShopCatalogBase`, `ShopCatalogDaily/Chest/Purchase/Gold`)는 `ShopCatalog.cs`에 분리되어 관리한다.
- row -> `ShopProductBase` 변환은 `ShopProductFactory`(15)에서 처리한다.

---

## 4. Implementation Location (3-path mirror)

클래스별 파일 분리 — `Product/` 폴더 (8개 파일):
- `ShopProductBase.cs`
- `ShopLimitedProductBase.cs`
- `ShopRewardProductBase.cs`
- `ShopProductDaily.cs`
- `ShopProductEvent.cs`
- `ShopProductGold.cs`
- `ShopProductChest.cs`
- `ShopProductPurchase.cs`

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Product/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Product/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/Shop/Product/`

---

## 5. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [15-shop-product-factory](../15-shop-product-factory/SKILL.md)
