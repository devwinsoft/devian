---
name: 13-shop-catalog
description: `SHOP_CATALOG_TYPE`와 카탈로그별 테이블 매핑(`DAILY/CHEST/PURCHASE/GOLD`) 정책을 정의할 때 사용한다.
---

# 13-shop-catalog

Status: ACTIVE
AppliesTo: v10

Shop Catalog는 상점 상품 소스를 카탈로그 타입으로 분리해서 관리한다.

구현 클래스는 `ShopCatalog.cs`에 분리한다.

---

## 1. Enum

`ENUM_META.json`

```json
SHOP_CATALOG_TYPE: [NONE, DAILY, CHEST, PURCHASE, GOLD]
SHOP_DISCOUNT_TYPE: [NONE, PER10, PER20, PER30, PER50]
SHOP_PRODUCT_TYPE: [NONE, FREE, ADS, CURRENCY, PURCHASE]
```

---

## 2. Table Mapping

- table file: `input/Domains/Game/ShopTable.xlsx`
- `SHOP_CATALOG_TYPE.DAILY` -> `SHOP_DAILY`
- `SHOP_CATALOG_TYPE.CHEST` -> `SHOP_CHEST`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `SHOP_PURCHASE`
- `SHOP_CATALOG_TYPE.GOLD` -> `SHOP_GOLD`

카탈로그 클래스 매핑:
- `SHOP_CATALOG_TYPE.DAILY` -> `ShopCatalogDaily`
- `SHOP_CATALOG_TYPE.CHEST` -> `ShopCatalogChest`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `ShopCatalogPurchase`
- `SHOP_CATALOG_TYPE.GOLD` -> `ShopCatalogGold`

카탈로그 초기화 라이프사이클:
- `ShopCatalogBase`는 생성자에서 product를 만들지 않는다.
- `Initialize()`가 1회 실행되며, 실제 생성은 `protected onInitialize()`에서 수행한다.
- `CreateDefaultCatalogs(...)`/`Create(...)`는 반환 전에 `Initialize()`를 호출해 product 인덱스를 확정한다.
- `CHEST/PURCHASE/GOLD`는 공통 row->product 빌더(`BuildProductsFromRows`)를 사용해 생성 로직을 단순화한다.
- `DAILY`를 제외한 카탈로그(`CHEST/PURCHASE/GOLD`)는 테이블의 모든 row를 상품으로 생성한다.
- `ShopCatalogBase`는 `virtual int autoRefreshDay`(기본 0), `RemainRefreshTimeMs`를 가진다.
- `autoRefreshDay`는 `DAILY=1`만 사용하고, `CHEST/PURCHASE/GOLD`는 기본값 `0`을 사용한다.

---

## 3. Reset Rule

- 구매 제한 상품 자동 리셋은 `DAILY` catalog의 `autoRefreshUtcMs`(다음 refresh 시각) 기준으로 처리한다.
- `DAILY` refresh 완료 시 다음 refresh 시각은 `serverNow + autoRefreshDay`로 갱신한다.
- `CHEST/GOLD/PURCHASE`는 `autoRefreshDay=0`이므로 `autoRefreshUtcMs` 저장/사용을 하지 않는다.
- ADS/FREE 상품 리필은 catalog 저장 버킷의 `adsRefreshUtcMs`(다음 ADS/FREE refill 시각) 기준으로 별도 처리한다.
- 카탈로그 초기화/refresh 시 `adsRefreshUtcMs`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
- ADS/FREE 구매 성공 시 `adsRefreshUtcMs = serverNow + 1day`를 기록한다.
- `SHOP_DAILY` 카탈로그 리셋 시에는 저장된 daily 동적 상태를 비우고 5개 선택 생성을 다시 수행한다.
- 카탈로그 리셋은 기본적으로 상품 카운트 상태를 갱신하며, `SHOP_DAILY`는 동적 상태 초기화 후 재선택 생성을 위해 카탈로그를 rebuild한다.
- 카탈로그별 리셋 남은 시간은 `ShopManager.GetAdsResetRemainingMs(catalogType)`로 조회한다.

## 4. SHOP_DAILY 선택 생성 규칙

- `SHOP_DAILY`는 초기화/리셋 시 전체 row를 그대로 쓰지 않고 ADS/FREE 제외 대상에서 `5개`를 선택 생성한다. (`const int`)
- `selectRate < 0` row는 무조건 포함한다.
- `selectRate > 0` row만 합산해 가중치 선택한다.
- `selectRate == 0` row는 선택 후보에서 제외한다.
- 동일 `shopId`(pk)는 중복 선택하지 않는다.
- 선택된 5개 중 무작위 3개를 할인 상품으로 선정한다. (중복 선정 금지)
- 할인 선정된 row는 `discountRate10Per/20Per/30Per/50Per` 합산 가중치로 `SHOP_DISCOUNT_TYPE(PER10/PER20/PER30/PER50)`를 결정한다.
- 할인 가중치 합이 0 이하면 `SHOP_DISCOUNT_TYPE.NONE`을 사용한다.
- 생성 결과는 storage에 `dailyCatalogProducts(shopId, discountType, remainCount)`로 저장한다. 저장 대상은 ADS/FREE 제외 5개 상품만이다.
- `SHOP_DAILY`의 ADS/FREE row는 고정 상품으로 카탈로그에 항상 포함하고, `dailyCatalogProducts`에는 저장하지 않는다.
- 저장된 daily 상태가 있으면 ADS/FREE 제외 5개를 저장 상태로 복원하고, ADS/FREE 고정 상품은 테이블에서 다시 합쳐 카탈로그를 구성한다.

## 5. Purchase Catalog Rule

- `catalog=PURCHASE` 상품은 `internalProductId`를 통해 `PurchaseManager`로 구매한다.
- 시즌 종료 임박 차단(`seasonId`) 검사는 ShopManager에서 수행한다.

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)

## 7. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopCatalog.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopCatalog.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopCatalog.cs`
