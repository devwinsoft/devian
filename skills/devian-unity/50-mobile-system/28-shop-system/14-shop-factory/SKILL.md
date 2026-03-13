---
name: 14-shop-factory
description: ShopProductFactory를 통해 테이블 row를 `ShopProductBase`로 변환하고, 카탈로그 구성은 `ShopCatalogBase` 계층에서 담당하도록 구현할 때 사용한다.
---

# 14-shop-factory

Status: ACTIVE
AppliesTo: v10

Shop Factory는 table row를 런타임 상품 모델로 변환하는 책임만 가진다.

---

## 1. Class

```csharp
public static class ShopProductFactory
```

- namespace: `Devian`
- 위치: `MobileSystem/Runtime/Shop/ShopProduct.cs`

---

## 2. Public Contract

```csharp
public static ShopProductBase CreateDailyProduct(SHOP_DAILY row, SHOP_DISCOUNT_TYPE discountType)
public static ShopProductBase CreateChestProduct(SHOP_CHEST row)
public static ShopProductBase CreatePurchaseProduct(SHOP_PURCHASE row)
public static ShopProductBase CreateGoldProduct(SHOP_GOLD row)
```

---

## 3. Table Mapping

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_CATALOG_TYPE.DAILY` -> `TB_SHOP_DAILY`
- `SHOP_CATALOG_TYPE.CHEST` -> `TB_SHOP_CHEST`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `TB_SHOP_PURCHASE`
- `SHOP_CATALOG_TYPE.GOLD` -> `TB_SHOP_GOLD`
- 카탈로그 구성(`DAILY/CHEST/PURCHASE/GOLD` 분기)은 `ShopCatalog.cs`의 `ShopCatalogBase` 파생 클래스가 담당한다.

---

## 4. Product Mapping Rule

- `SHOP_PURCHASE` row -> `ShopProductPurchase`
- reward row(`SHOP_DAILY/CHEST/GOLD`)는 `currencyType`으로 분기:
- `FREE` -> `ShopProductFree`
- `ADS` -> `ShopProductAds`
- 그 외 -> `ShopProductCurrency`
- row의 `price`는 `ShopProductBase.PriceWithoutDiscount`로 저장되고, 런타임 구매 계산은 `ShopProductBase.Price`(할인 반영)로 처리한다.
- reward row의 `maxCount`는 테이블 값(`row.MaxCount`)을 사용한다. 값이 없거나 음수면 `-1`(무제한)로 해석한다.
- `SHOP_GOLD`는 `amount`가 없으므로 `Amount=1`로 고정
- `SHOP_DAILY` row -> `ShopProductFactory.CreateDailyProduct(row, discountType)`
- `SHOP_CHEST` row -> `ShopProductFactory.CreateChestProduct(row)`
- `SHOP_PURCHASE` row -> `ShopProductFactory.CreatePurchaseProduct(row)`
- `SHOP_GOLD` row -> `ShopProductFactory.CreateGoldProduct(row)`
- `SHOP_PURCHASE`는 런타임 제한을 두지 않으므로 `maxCount=-1`로 생성한다.

---

## 5. Hard Rules

- Factory는 row 변환만 담당하고, 카탈로그 단위 선택/분기/인덱싱은 `ShopCatalog.cs`/`ShopManager`에서 담당한다.

---

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
