---
name: 13-shop-catalog
description: `SHOP_CATALOG` 기반 카탈로그 생성/초기화/잠금 해금과 DAILY snapshot 규칙을 정의할 때 사용한다.
---

# 13-shop-catalog

Status: ACTIVE
AppliesTo: v10

Shop Catalog는 상점 상품 소스를 카탈로그 타입으로 분리해서 관리한다.

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
- 카탈로그 클래스 매핑:
  - `DAILY` -> `ShopCatalogDaily`
  - `CHEST` -> `ShopCatalogChest`
  - `PURCHASE` -> `ShopCatalogPurchase`
  - `GOLD` -> `ShopCatalogGold`
  - `EVENT` -> `ShopCatalogEvent`
- 카탈로그 상품 테이블 매핑:
  - `DAILY` -> `SHOP_ITEM_DAILY`
  - `CHEST` -> `SHOP_ITEM_CHEST`
  - `PURCHASE` -> `SHOP_ITEM_PURCHASE`
  - `GOLD` -> `SHOP_ITEM_GOLD`
  - `EVENT` -> `SHOP_ITEM_EVENT`
  - `CHEST` progression source -> `SHOP_CATALOG_CHEST`

카탈로그 초기화 라이프사이클:

- `ShopCatalogBase`는 생성자에서 product를 만들지 않는다.
- `Initialize()`가 1회 실행되며, `onInitialize()`로 1회 setup 후 `RefreshProducts()`를 호출한다.
- `CreateRuntimeCatalogs(storage)`는 `TB_SHOP_CATALOG.GetAll()`을 읽어 storage-backed runtime catalog를 생성한다.
- `CreateRuntimeCatalogs(storage)`는 catalog 인스턴스만 만들고, `ShopManager.Initialize()` 경로에서 registry 등록 후 각 catalog `Initialize()`를 호출한다.
- `Create(...)`는 standalone catalog 생성을 위해 반환 전에 `Initialize()`를 호출해 product 인덱스를 확정한다.
- `ShopCatalogBase.onRefresh()` 기본 구현은 `CHEST/PURCHASE/GOLD`의 테이블 전체 row를 상품으로 생성하고, 해당 catalog bucket의 remain state를 적용한다.
- `ShopCatalogDaily.onRefresh()`는 DAILY snapshot 복원 또는 재생성을 처리한다.
- `ShopCatalogEvent.onRefresh()`는 `SHOP_ITEM_EVENT.start_time/end_time` 서버 UTC 구간 안의 row만 상품으로 생성한다.
- 각 catalog 생성자는 자기 전용 `ShopCatalog*StorageData`를 받는다.
- row -> `ShopProductBase` 변환은 `ShopProductFactory`(15)에서 처리한다.

---

## 3. Refresh Rule

- 일반 카탈로그의 구매 제한 자동 refresh는 `autoRefreshUtcMs`(다음 refresh 시각) + `auto_refresh_days` 기준으로 처리한다.
- 일반 카탈로그는 `auto_refresh_days <= 0`이면 자동 refresh를 사용하지 않는다.
- 일반 카탈로그 refresh 완료 시 다음 refresh 시각은 `serverNow + auto_refresh_days`로 갱신한다.
- `EVENT`는 `auto_refresh_days` 대신 가장 가까운 미래 `start_time/end_time` 경계 시각을 다음 refresh 시각으로 사용한다.
- ADS/FREE 상품 리필은 catalog 저장 버킷의 `adsRefreshUtcMs` 기준으로 별도 처리한다.
- 카탈로그 초기화/refresh 시 `adsRefreshUtcMs`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=max_count`로 리필한다.
- ADS/FREE 구매 성공 시 `adsRefreshUtcMs = serverNow + 1day`를 기록한다.
- `SHOP_ITEM_DAILY` 카탈로그 refresh 시에는 저장된 DAILY snapshot을 새 snapshot으로 교체한다.
- refresh 조건 탐색은 `ShopManager.evaluateCatalogRefreshState(...)` 한 곳에서 처리한다.
- 카탈로그별 refresh 남은 시간은 catalog runtime 프로퍼티로 조회한다. 공통 값은 `RemainAutoRefreshTimeMs`, DAILY 전용 값은 `RemainAdsRefreshTimeMs`, `ManualRefreshRemainTimeMs`, `ManualRefreshRemainCount`를 사용한다.

---

## 4. Unlock Rule

- `SHOP_CATALOG.unlock_msg_id`가 비어있으면 잠금 없이 사용한다.
- `SHOP_CATALOG.unlock_msg_id`가 `IsNullOrWhiteSpace`면 unlock 조건 없음으로 간주하며 `IsLocked=false`다.
- `unlock_msg_id`가 있으면 `IsLocked=true`로 시작한다.
- `ShopManager`는 `GameMessageManager`를 구독하고 `unlock_op_type/unlock_value` 조건을 평가해 해금한다.
- 해금 조건 평가는 누적 stat(`GameMessageManager.GetStat(unlock_msg_id)`)으로 수행한다.
- 조건 만족 시 `IsLocked=false`로 전환된다.
- 잠긴 카탈로그의 상품은 `CanBuy/BuyAsync`에서 차단된다.

---

## 5. SHOP_ITEM_DAILY Snapshot Rule

- `SHOP_ITEM_DAILY`는 초기화/refresh 시 최종 snapshot을 생성한다.
- `currency_type=FREE/ADS` row는 고정 상품으로 항상 포함한다.
- `currency_type!=FREE/ADS` row에서 `5개`를 선택 생성한다.
- 이 규칙은 `auto_refresh_days` 값과 무관하며 `DAILY`의 고유 생성 규칙이다.
- `select_rate < 0` row는 무조건 포함한다.
- `select_rate > 0` row만 합산해 가중치 선택한다.
- `select_rate == 0` row는 선택 후보에서 제외한다.
- 동일 `shop_item_id`는 중복 선택하지 않는다.
- 선택된 비고정 5개 중 무작위 3개를 할인 상품으로 선정한다.
- 할인 선정된 row는 `discount_rate10_per/20Per/30Per/50Per` 합산 가중치로 `SHOP_DISCOUNT_TYPE(PER10/PER20/PER30/PER50)`를 결정한다.
- 할인 가중치 합이 0 이하면 `SHOP_DISCOUNT_TYPE.NONE`을 사용한다.
- 각 snapshot row는 `amount_min/amount_max` 범위에서 runtime `amount`를 1회 선택한다.
- 생성 결과는 storage의 `dailyCatalogProducts(shopId, discountType, remainCount, amount)`로 저장한다.
- `dailyCatalogProducts`는 DAILY snapshot 전체를 저장한다. `FREE/ADS`도 동일 리스트에 포함한다.
- 저장된 DAILY snapshot이 있으면 그 상태를 복원한다. legacy save처럼 `FREE/ADS`가 빠져 있으면 fixed row를 보강해 정규화한다.
- 저장된 DAILY snapshot의 만료 여부는 `ShopManager`의 시간 기반 refresh 판정에서 결정한다.
- `SHOP_ITEM_DAILY.unit_price`는 단가다. `ShopProductDaily.PriceWithoutDiscount`는 `unit_price * snapshot.amount`이며, 할인은 그 결과에 적용한다.
- daily runtime `amount`는 reward 반복 횟수이면서 가격 배수에도 반영된다.
- daily manual refresh는 광고 시청 성공으로만 가능하며, rolling 24시간 기준 최대 5회다.
- daily manual refresh의 상태 판단, 광고 호출, 성공 시 `manualRefreshUtcMs/manualRefreshRemainCount/autoRefreshUtcMs` 갱신은 `ShopCatalogDaily`가 직접 처리한다.
- `manualRefreshRemainCount`는 남은 횟수이며, 초기값과 만료 후 reset 값은 5다.

---

## 6. Event Catalog Rule

- `catalog=EVENT` 상품 소스는 `SHOP_ITEM_EVENT`다.
- `ShopCatalogEvent.onRefresh()`는 서버 UTC 현재 시각이 `start_time <= now < end_time`인 row만 생성한다.
- 다음 refresh 시각은 가장 가까운 미래 `start_time/end_time` 경계 시각이다.
- `EVENT`는 DAILY처럼 별도 동적 상품 payload를 저장하지 않는다.

---

## 7. Purchase Catalog Rule

- `catalog=PURCHASE` 상품은 `internal_product_id`를 통해 `PurchaseManager`로 구매한다.
- 시즌 종료 임박 차단(`season_id`) 검사는 ShopManager에서 수행한다.

---

## 8. Chest Catalog Rule

- `catalog=CHEST` 상품 소스는 `SHOP_ITEM_CHEST`다.
- chest progression source는 `SHOP_CATALOG_CHEST`다.
- chest 구매 reward는 `SHOP_ITEM_CHEST.reward_group_id`가 아니라 현재 chest level row의 `reward_ads/reward_paid01/reward_paid10`에서 결정한다.
- chest exp는 현재 chest level row의 `ads_exp/gain_exp01/gain_exp10`을 사용한다.
- `SHOP_ITEM_CHEST.amount`는 reward 반복 지급 횟수이며 가격에는 곱하지 않는다.
- 최대 레벨은 `SHOP_CATALOG_CHEST`의 최대 `Level` 값이며, 최대 레벨에서는 `CurrentExp=0`이고 추가 exp를 획득하지 않는다.

---

## 9. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Shop/Catalog/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/Shop/Catalog/`

파일 구성:

- `ShopCatalogBase.cs` — abstract base, runtime helpers
- `ShopCatalogFactory.cs` — catalog instance 생성 factory
- `ShopCatalogEmpty.cs` — empty placeholder (internal)
- `ShopCatalogDaily.cs` — `FREE/ADS + 비고정 5개` snapshot 생성/할인/저장 복원
- `ShopCatalogEvent.cs` — 시간 구간 필터링
- `ShopCatalogChest.cs` — CHEST 카탈로그
- `ShopCatalogPurchase.cs` — PURCHASE 카탈로그
- `ShopCatalogGold.cs` — GOLD 카탈로그

---

## 10. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
- [15-shop-product-factory](../15-shop-product-factory/SKILL.md)
