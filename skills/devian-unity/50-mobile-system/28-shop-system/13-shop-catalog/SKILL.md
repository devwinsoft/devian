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

- FREE/ADS + 구매 제한 상품 리셋은 카탈로그별 기준 시각 + 24시간 경과 기준이다.
- FREE/ADS 요청이 성공했고 남은 리셋 시간이 0이면, 해당 시각을 카탈로그 기준 시각으로 기록한다.
- 비 ADS + 구매 제한 상품 리셋은 전역 1일(UTC day start) 기준이다.
- 카탈로그 리셋은 상품 카운트 상태만 갱신하며 테이블 재로드를 강제하지 않는다.
- 카탈로그별 FREE/ADS 리셋 남은 시간은 `ShopManager.GetAdsResetRemainingMs(catalogType)`로 조회한다.

## 4. SHOP_DAILY 선택 생성 규칙

- `SHOP_DAILY`는 초기화/리셋 시 전체 row를 그대로 쓰지 않고 `5개`를 선택 생성한다. (`const int`)
- `selectRate < 0` row는 무조건 포함한다.
- `selectRate > 0` row만 합산해 가중치 선택한다.
- `selectRate == 0` row는 선택 후보에서 제외한다.
- 동일 `shopId`(pk)는 중복 선택하지 않는다.

## 5. Purchase Catalog Rule

- `catalog=PURCHASE` 상품은 `internalProductId`를 통해 `PurchaseManager`로 구매한다.
- 시즌 종료 임박 차단(`seasonId`) 검사는 ShopManager에서 수행한다.

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
