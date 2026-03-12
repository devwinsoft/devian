---
name: 14-shop-factory
description: ShopProductFactory를 통해 `SHOP_CATALOG_TYPE`별 테이블(`SHOP_DAILY/CHEST/PURCHASE/GOLD`) row를 `ShopProductBase` 계층으로 생성/매핑할 때 사용한다.
---

# 14-shop-factory

Status: ACTIVE
AppliesTo: v10

Shop Factory는 카탈로그 소스 테이블을 런타임 상품 모델로 변환하는 책임을 가진다.

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
public static ShopCatalog BuildCatalog(SHOP_CATALOG_TYPE catalogType)
public static IReadOnlyList<ShopProductBase> BuildCatalogProducts(SHOP_CATALOG_TYPE catalogType)
public static ShopProductBase Get(string shopId)
public static bool TryGet(string shopId, out ShopProductBase product)
```

---

## 3. Table Mapping

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_CATALOG_TYPE.DAILY` -> `TB_SHOP_DAILY`
- `SHOP_CATALOG_TYPE.CHEST` -> `TB_SHOP_CHEST`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `TB_SHOP_PURCHASE`
- `SHOP_CATALOG_TYPE.GOLD` -> `TB_SHOP_GOLD`

---

## 4. Product Mapping Rule

- `SHOP_PURCHASE` row -> `ShopProductPurchase`
- reward row(`SHOP_DAILY/CHEST/GOLD`)는 `currencyType`으로 분기:
- `FREE` -> `ShopProductFree`
- `ADS` -> `ShopProductAds`
- 그 외 -> `ShopProductCurrency`
- `SHOP_GOLD`는 `amount`가 없으므로 `Amount=1`로 고정
- `SHOP_DAILY`는 초기화/리셋 시 5개 선택 생성:
- `SHOP_DAILY` 선택 규칙: `selectRate < 0` 무조건 포함
- `SHOP_DAILY` 선택 규칙: `selectRate > 0` 합산 rate 기반 가중치 선택
- `SHOP_DAILY` 선택 규칙: `selectRate == 0` 선택 후보 제외
- `SHOP_DAILY` 선택 규칙: 동일 `shopId`(pk) 중복 선택 금지

---

## 5. Hard Rules

- `shopId`는 trim 후 lookup한다.
- 미존재 `shopId`는 `null`을 반환한다.
- Factory는 데이터 생성만 담당하고 구매 가능 여부/차감/지급은 `ShopManager`가 담당한다.

---

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
