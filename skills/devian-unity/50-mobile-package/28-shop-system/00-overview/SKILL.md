# 28-shop-system — Overview

Status: ACTIVE
AppliesTo: v10

Shop System은 `SHOP_CATALOG_TYPE` 기반 카탈로그(`DAILY/CHEST/PURCHASE/GOLD/EVENT`)를 관리하고,
`shop_item_id` 기준 구매/제한/refresh 정책을 처리한다.
카탈로그 인스턴스는 하드코딩이 아니라 `SHOP_CATALOG` 테이블 row를 읽어서 생성한다.
일반 카탈로그의 자동 refresh는 `SHOP_CATALOG.auto_refresh_days`를 사용한다.
`SHOP_CATALOG.unlock_msg_id`가 설정된 카탈로그는 초기 `IsLocked=true`로 시작하며,
`unlock_op_type/unlock_value` 조건을 `GameMessageManager` 누적 stat으로 평가해 해금한다.
`SHOP_ITEM_DAILY`는 초기화/refresh 시 `currency_type=FREE/ADS` 상품을 항상 포함하고, 나머지 대상에서 `select_rate` 규칙으로 5개 상품을 선택해 최종 snapshot을 생성한다.
`CHEST/PURCHASE/GOLD`는 각 `SHOP_*` 테이블의 모든 상품을 생성한다.
`EVENT`는 `SHOP_ITEM_EVENT.start_time/end_time` 서버 UTC 시간 구간 안에 있는 상품만 생성한다.
`SHOP_ITEM_DAILY`는 정적 row에 `amount_min/amount_max`를 두고, 초기화/refresh 시 min/max 범위에서 runtime `amount`를 뽑아 snapshot에 저장한다.
`SHOP_ITEM_DAILY.price`는 단가이며, 실제 구매 가격은 `price * snapshot.amount`에 discount를 적용한 값이다.
`SHOP_ITEM_DAILY`의 최종 snapshot(FREE/ADS 포함, remainCount/discountType/amount 포함)은 `daily.dailyCatalogProducts` 하나에 저장한다.
일반 카탈로그의 구매 제한 자동 refresh는 카탈로그 저장 버킷의 `autoRefreshUtcMs`(다음 refresh 시각) + `auto_refresh_days` 규칙으로 처리된다.
`EVENT`는 `auto_refresh_days` 대신 다음 `start_time/end_time` 경계 시각을 `autoRefreshUtcMs`로 저장해 refresh를 예약한다.
ADS/FREE 상품 리필은 카탈로그별 저장 버킷의 `adsRefreshUtcMs`(다음 ADS/FREE refill 시각)으로 별도 관리한다.
ADS/FREE 구매 성공 시 `adsRefreshUtcMs`는 `serverNow + 1day`로 기록된다.
`SHOP_ITEM_DAILY`는 광고 시청 성공 시 `ShopCatalogDaily.RefreshByAdsAsync()`로 수동 refresh할 수 있으며, rolling 24시간 기준 최대 5회(`manualRefreshUtcMs`, `manualRefreshRemainCount`)를 사용한다. `manualRefreshRemainCount`는 남은 횟수이며 초기값은 5다.
`SHOP_ITEM_DAILY` 카탈로그 refresh 시에는 저장된 daily snapshot을 새 snapshot으로 교체한다.
카탈로그별 남은 시간 조회는 catalog runtime 프로퍼티(`RemainAutoRefreshTimeMs`, `RemainAdsRefreshTimeMs`, `ManualRefreshRemainTimeMs`)를 사용한다.
`ShopCatalogBase`는 storage-backed catalog runtime helper와 공통 refresh helper를 소유하고,
`ShopCatalogDaily`는 manual refresh 상태 판단/광고 시청/성공 기록을 직접 처리한다.
`ShopManager`는 global initialize/global refresh/product index/local save queue만 담당한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [03-ssot](../03-ssot/SKILL.md) | Shop catalog initialize/refresh operation 정본 |
| [10-shop-manager](../10-shop-manager/SKILL.md) | ShopManager API/구현 규약 |
| [12-shop-storage](../12-shop-storage/SKILL.md) | ShopStorage 저장 규약 |
| [13-shop-catalog](../13-shop-catalog/SKILL.md) | 카탈로그 구성/선택 생성 규약 |
| [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md) | ShopCatalogFactory 카탈로그 인스턴스 생성 |
| [15-shop-product-factory](../15-shop-product-factory/SKILL.md) | ShopProductFactory row→product 변환 |
| [16-shop-catalog-chest](../16-shop-catalog-chest/SKILL.md) | Chest catalog level/exp/reward routing 규약 |

---

## Sub-skills

- [03-ssot](../03-ssot/SKILL.md)
- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
- [15-shop-product-factory](../15-shop-product-factory/SKILL.md)
- [16-shop-catalog-chest](../16-shop-catalog-chest/SKILL.md)
