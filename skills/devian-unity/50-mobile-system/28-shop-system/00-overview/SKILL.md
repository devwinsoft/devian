# 28-shop-system — Overview

Status: ACTIVE
AppliesTo: v10

Shop System은 `SHOP_CATALOG_TYPE` 기반 카탈로그(`DAILY/CHEST/PURCHASE/GOLD`)를 관리하고,
`shopId` 기준 구매/제한/리셋 정책을 처리한다.
`SHOP_DAILY`는 초기화/리셋 시 `selectRate` 규칙으로 5개 상품을 선택 생성한다.
구매 제한 리셋은 카탈로그별 기준 시각 기준 24시간 롤링이다.
구매 제한 상품 요청이 성공했고 카탈로그 리셋 남은 시간이 0이면 해당 시각을 카탈로그 기준 시각으로 기록한다.
`SHOP_DAILY` 카탈로그 리셋 시에는 저장된 daily 동적 상태를 비우고 5개 선택 생성을 다시 수행한다.
필요 시 `ShopManager.GetAdsResetRemainingMs(SHOP_CATALOG_TYPE)`로 카탈로그별 리셋 남은 시간(ms)을 조회한다.

---

## Sub-skills

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-factory](../14-shop-factory/SKILL.md)
