# 28-shop-system — Overview

Status: ACTIVE
AppliesTo: v10

Shop System은 `SHOP_CATALOG_TYPE` 기반 카탈로그(`DAILY/CHEST/PURCHASE/GOLD`)를 관리하고,
`shopId` 기준 구매/제한/리셋 정책을 처리한다.
`SHOP_DAILY`는 초기화/리셋 시 `selectRate` 규칙으로 5개 상품을 선택 생성한다.
FREE/ADS 구매 제한 리셋은 카탈로그별 기준 시각 기준 24시간 롤링이며, FREE/ADS 요청 성공 시 남은 시간이 0이면 해당 시각을 기준으로 기록한다.
비 ADS 구매 제한 리셋은 전역 1일(UTC day start) 정책을 사용한다.
필요 시 `ShopManager.GetAdsResetRemainingMs(SHOP_CATALOG_TYPE)`로 카탈로그별 FREE/ADS 리셋 남은 시간(ms)을 조회한다.

---

## Sub-skills

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
