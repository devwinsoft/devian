---
name: 13-shop-catalog
description: `SHOP_CATALOG` 테이블 기반 카탈로그 생성/초기화/잠금 해금(`unlockMsgId`) 정책을 정의할 때 사용한다.
---

# 13-shop-catalog

Status: ACTIVE
AppliesTo: v10

Shop Catalog는 상점 상품 소스를 카탈로그 타입으로 분리해서 관리한다.

구현 클래스는 `Shop/Catalog/` 폴더에 클래스별 파일로 분리한다.

`catalog initialize/refresh operation`의 정본은 [03-ssot](../03-ssot/SKILL.md)다.
이 문서는 카탈로그 데이터 해석 규칙을 다룬다.

---

## 1. Enum

`ENUM_META.json`

```json
SHOP_CATALOG_TYPE: [NONE, DAILY, CHEST, PURCHASE, GOLD, EVENT]
SHOP_DISCOUNT_TYPE: [NONE, PER10, PER20, PER30, PER50]
SHOP_PRODUCT_TYPE: [NONE, FREE, ADS, CURRENCY, PURCHASE]
```

---

## 2. Table Mapping

- table file: `input/Domains/Game/ShopTable.xlsx`
- 카탈로그 생성 소스: `SHOP_CATALOG`
- `SHOP_CATALOG` 필드:
- `catalogType` (`SHOP_CATALOG_TYPE`)
- `nameId`
- `autoRefreshDays`
- `unlockMsgId`
- `unlockOpType` (`GAME_MESSAGE_OP_TYPE`)
- `unlockValue` (`CBigInt`)

카탈로그 클래스 매핑:
- `SHOP_CATALOG_TYPE.DAILY` -> `ShopCatalogDaily`
- `SHOP_CATALOG_TYPE.CHEST` -> `ShopCatalogChest`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `ShopCatalogPurchase`
- `SHOP_CATALOG_TYPE.GOLD` -> `ShopCatalogGold`
- `SHOP_CATALOG_TYPE.EVENT` -> `ShopCatalogEvent`

카탈로그 상품 테이블 매핑:
- `SHOP_CATALOG_TYPE.DAILY` -> `SHOP_DAILY`
- `SHOP_CATALOG_TYPE.CHEST` -> `SHOP_CHEST`
- `SHOP_CATALOG_TYPE.PURCHASE` -> `SHOP_PURCHASE`
- `SHOP_CATALOG_TYPE.GOLD` -> `SHOP_GOLD`
- `SHOP_CATALOG_TYPE.EVENT` -> `SHOP_EVENT`

카탈로그 초기화 라이프사이클:
- `ShopCatalogBase`는 생성자에서 product를 만들지 않는다.
- `Initialize()`가 1회 실행되며, `onInitialize()`로 1회 setup 후 `RefreshProducts()`를 호출한다.
- `CreateDefaultCatalogs(...)`는 `TB_SHOP_CATALOG.GetAll()`을 읽어 카탈로그를 생성한다. (하드코딩 생성 금지)
- `CreateDefaultCatalogs(...)`는 catalog 인스턴스만 만들고, `ShopManager.Initialize()` 경로에서 각 catalog `Initialize()`를 호출한다.
- `Create(...)`는 standalone catalog 생성을 위해 반환 전에 `Initialize()`를 호출해 product 인덱스를 확정한다.
- `ShopCatalogBase.onInitialize()`는 1회 setup hook이다. product 생성 책임을 가지지 않는다.
- `ShopCatalogBase.onRefresh()` 기본 구현은 `CHEST/PURCHASE/GOLD`의 테이블 전체 row를 상품으로 생성한다.
- `ShopCatalogDaily.onRefresh()`는 5개 선택 생성/저장 상태 복원을 처리한다.
- `ShopCatalogEvent.onRefresh()`는 `SHOP_EVENT.startTime/endTime` 서버 UTC 구간 안에 있는 row만 상품으로 생성한다.
- row -> `ShopProductBase` 변환 helper도 `ShopCatalogBase` 내부에 둔다. 별도 factory 계층을 두지 않는다.
- `CHEST/PURCHASE/GOLD`는 테이블의 모든 row를 상품으로 생성한다.
- `ShopCatalogBase`는 `Storage`, `virtual int autoRefreshDays`, `RemainAutoRefreshTimeMs`, `RemainAdsRefreshTimeMs`, `IsLocked`를 가진다.
- catalog-specific public operation은 catalog instance method로 둔다.
- `ShopCatalogBase.ResetAds()`가 공통 강제 refresh 진입점이다.
- `ShopCatalogDaily.RefreshByAdsAsync()`가 DAILY 수동 refresh 진입점이다.
- `autoRefreshDays`는 `SHOP_CATALOG.autoRefreshDays` 값을 사용한다.
- `unlockMsgId`가 비어있지 않으면 초기 `IsLocked=true`다.

---

## 3. Refresh Rule

- 일반 카탈로그의 구매 제한 자동 refresh는 각 카탈로그의 `autoRefreshUtcMs`(다음 refresh 시각) + `autoRefreshDays` 기준으로 처리한다.
- 일반 카탈로그는 `autoRefreshDays <= 0`이면 자동 refresh를 사용하지 않는다.
- 일반 카탈로그 refresh 완료 시 다음 refresh 시각은 `serverNow + autoRefreshDays`로 갱신한다.
- `EVENT`는 `autoRefreshDays`를 사용하지 않고, 가장 가까운 미래 `startTime/endTime` 경계 시각을 다음 refresh 시각으로 사용한다.
- ADS/FREE 상품 리필은 catalog 저장 버킷의 `adsRefreshUtcMs`(다음 ADS/FREE refill 시각) 기준으로 별도 처리한다.
- 카탈로그 초기화/refresh 시 `adsRefreshUtcMs`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
- ADS/FREE 구매 성공 시 `adsRefreshUtcMs = serverNow + 1day`를 기록한다.
- `SHOP_DAILY` 카탈로그 refresh 시에는 저장된 daily 동적 상태를 비우고 5개 선택 생성을 다시 수행한다.
- refresh 조건 탐색은 `ShopManager.evaluateCatalogRefreshState(...)` 한 곳에서 처리한다.
- 카탈로그별 refresh 남은 시간은 catalog runtime 프로퍼티(`RemainAutoRefreshTimeMs`, `RemainAdsRefreshTimeMs`, `RemainManualRefreshTimeMs`)로 조회한다.

## 4. Unlock Rule

- `SHOP_CATALOG.unlockMsgId`가 비어있으면 잠금 없이 사용한다.
- `SHOP_CATALOG.unlockMsgId`가 `IsNullOrWhiteSpace`면 unlock 조건 없음으로 간주하며 `IsLocked=false`다.
- `unlockMsgId`가 있으면 `IsLocked=true`로 시작한다.
- `ShopManager`는 `GameMessageManager`를 구독하고 `unlockOpType/unlockValue` 조건을 평가해 해금한다.
- 해금 조건 평가는 누적 stat(`GameMessageManager.GetStat(unlockMsgId)`)으로 수행한다.
- 조건 만족 시 `IsLocked=false`로 전환된다.
- 잠긴 카탈로그의 상품은 `CanBuy/BuyAsync`에서 차단된다.

## 5. SHOP_DAILY 선택 생성 규칙

- `SHOP_DAILY`는 초기화/refresh 시 전체 row를 그대로 쓰지 않고 ADS/FREE 제외 대상에서 `5개`를 선택 생성한다. (`const int`)
- 이 규칙은 `autoRefreshDays` 값과 무관하며, `DAILY`의 고유 생성 규칙이다.
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
- 저장된 daily 상태의 만료 여부는 `ShopManager`의 시간 기반 refresh 판정에서 결정한다.
- `ShopCatalogDaily`는 `RemainManualRefreshTimeMs`, `RemainManualRefreshCount`를 가진다.
- daily manual refresh는 광고 시청 성공으로만 가능하며, rolling 24시간 기준 최대 5회다.
- daily manual refresh의 상태 판단, 광고 호출, 성공 시 `manualRefreshUtcMs/manualRefreshCount/autoRefreshUtcMs` 갱신은 `ShopCatalogDaily`가 직접 처리한다.

## 6. Event Catalog Rule

- `catalog=EVENT` 상품 소스는 `SHOP_EVENT`다.
- `ShopCatalogEvent.onRefresh()`는 서버 UTC 현재 시각이 `startTime <= now < endTime`인 row만 생성한다.
- 다음 refresh 시각은 가장 가까운 미래 `startTime/endTime` 경계 시각이다.
- `EVENT`는 DAILY처럼 별도 동적 상품 payload를 저장하지 않는다.

## 7. Purchase Catalog Rule

- `catalog=PURCHASE` 상품은 `internalProductId`를 통해 `PurchaseManager`로 구매한다.
- 시즌 종료 임박 차단(`seasonId`) 검사는 ShopManager에서 수행한다.

## 8. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)

## 9. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/Catalog/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/Catalog/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/Catalog/`

파일 구성:
- `ShopCatalogBase.cs` — abstract base, factory methods, product helpers
- `ShopCatalogEmpty.cs` — empty placeholder (internal)
- `ShopCatalogDaily.cs` — 5개 선택 생성/할인/저장 복원
- `ShopCatalogEvent.cs` — 시간 구간 필터링
- `ShopCatalogChest.cs` — CHEST 카탈로그
- `ShopCatalogPurchase.cs` — PURCHASE 카탈로그
- `ShopCatalogGold.cs` — GOLD 카탈로그
