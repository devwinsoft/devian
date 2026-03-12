---
name: 13-shop-catalog
description: `SHOP_CATALOG_TYPE`와 카탈로그별 테이블 매핑(`DAILY/CHEST/PURCHASE/GOLD`) 정책을 정의할 때 사용한다.
---

# 13-shop-catalog

Status: ACTIVE
AppliesTo: v10

Shop Catalog는 상점 상품 소스를 카탈로그 타입으로 분리해서 관리한다.

---

## 1. Enum

`ENUM_META.json`

```json
SHOP_CATALOG_TYPE: [NONE, DAILY, CHEST, PURCHASE, GOLD]
SHOP_PRODUCT_TYPE: [NONE, FREE, ADS, CURRENCY, PURCHASE]
```

---

## 2. Table Mapping

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_CATALOG_TYPE.DAILY` -> `SHOP_DAILY`
- `SHOP_CATALOG_TYPE.CHEST` -> `SHOP_CHEST`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `SHOP_PURCHASE`
- `SHOP_CATALOG_TYPE.GOLD` -> `SHOP_GOLD`

---

## 3. Reset Rule

- Shop 리셋은 전역 1일 고정이다.
- 리셋 시 카탈로그별 테이블을 다시 로드하여 상품 목록을 재구성한다.
- 현재 구현은 "테이블 전체 로드" 방식이다.

---

## 4. Purchase Catalog Rule

- `catalog=PURCHASE` 상품은 `internalProductId`를 통해 `PurchaseManager`로 구매한다.
- 시즌 종료 임박 차단(`seasonId`) 검사는 ShopManager에서 수행한다.

---

## 5. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
