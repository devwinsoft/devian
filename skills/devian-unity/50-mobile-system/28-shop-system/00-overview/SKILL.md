# 28-shop-system — Overview

Status: ACTIVE
AppliesTo: v10

Shop System은 `SHOP_CATALOG_TYPE` 기반 카탈로그(`DAILY/CHEST/PURCHASE/GOLD`)를 관리하고,
`shopId` 기준 구매/제한/리셋 정책을 처리한다.
`SHOP_DAILY`는 초기화/리셋 시 `selectRate` 규칙으로 ADS/FREE 제외 대상에서 5개 상품을 선택 생성한다.
`DAILY` 이외 카탈로그(`CHEST/PURCHASE/GOLD`)는 테이블의 모든 상품을 생성한다.
`SHOP_DAILY`의 ADS/FREE 상품은 고정 상품으로 항상 테이블에서 로드되며, 5개 선택 대상/저장 대상에 포함되지 않는다.
구매 제한 자동 리셋은 `DAILY` 저장 버킷의 `autoRefreshUtcMs`(다음 refresh 시각) + `autoRefreshDay` 규칙으로 처리된다.
ADS/FREE 상품 리필은 카탈로그별 저장 버킷의 `adsRefreshUtcMs`(다음 ADS/FREE refill 시각)으로 별도 관리한다.
ADS/FREE 구매 성공 시 `adsRefreshUtcMs`는 `serverNow + 1day`로 기록된다.
`SHOP_DAILY` 카탈로그 리셋 시에는 저장된 daily 동적 상태를 비우고 5개 선택 생성을 다시 수행한다.
필요 시 `ShopManager.GetAdsResetRemainingMs(SHOP_CATALOG_TYPE)`로 카탈로그별 리셋 남은 시간(ms)을 조회한다.

---

## Sub-skills

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
