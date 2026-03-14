---
name: 10-shop-manager
description: MobileSystem ShopManager를 CompoSingleton으로 구현하고 `SHOP_CATALOG` 기반 카탈로그 생성/refresh/잠금해금(`unlockMsgId`) + 구매(`CanBuy`, `BuyAsync`) 플로우를 적용할 때 사용한다.
---

# 10-shop-manager

Status: ACTIVE
AppliesTo: v10

ShopManager는 MobileSystem 상점 구매 진입점이며, 카탈로그별 상품 목록을 관리한다.

`catalog initialize/refresh operation`의 정본은 [03-ssot](../03-ssot/SKILL.md)다.
이 문서는 API/구현 포인트를 다루며, 동작 순서 충돌 시 SSOT가 우선한다.

---

## 1. Class

```csharp
public sealed class ShopManager : CompoSingleton<ShopManager>
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobileSystem`
- `MobileApplication`에서 `RequireComponent(typeof(ShopManager))`로 보장한다.

---

## 2. Public API

```csharp
public bool CanBuy(string shopId)
public Task<CommonResult<RewardData[]>> BuyAsync(string shopId, CancellationToken ct = default)
public CommonResult Initialize()
public CommonResult RefreshProducts(bool requireServerTime = true)
public IReadOnlyList<ShopCatalogBase> GetCatalogs()
public ShopCatalogBase GetCatalog(SHOP_CATALOG_TYPE catalogType)
public T GetCatalog<T>() where T : ShopCatalogBase
```

핵심 플로우:

1. `shopId`로 `ShopProductBase` 조회
2. 카탈로그 목록은 하드코딩이 아니라 `TB_SHOP_CATALOG.GetAll()` row로 생성된다.
3. `SHOP_DAILY` 카탈로그는 초기화/refresh 시 `selectRate` 규칙으로 ADS/FREE 제외 대상에서 5개 선택 생성된다.
4. `CHEST/PURCHASE/GOLD`는 각 `SHOP_*` 테이블의 모든 상품을 생성한다.
5. `EVENT`는 `SHOP_EVENT.startTime/endTime` 서버 UTC 구간 안에 있는 상품만 생성한다.
6. `SHOP_DAILY` 선택 생성에서 동일 `shopId`(pk)는 중복 선택하지 않는다.
7. 구매 제한 자동 refresh 체크(서버 시간 기준): 일반 카탈로그는 `autoRefreshUtcMs` + `autoRefreshDays` 규칙을 사용한다. `autoRefreshDays <= 0`이면 자동 refresh를 사용하지 않는다.
8. `EVENT`는 `autoRefreshDays`가 아니라 가장 가까운 미래 `startTime/endTime` 경계 시각을 `autoRefreshUtcMs`로 저장해 refresh를 예약한다.
9. `unlockMsgId`가 있는 카탈로그는 초기 `IsLocked=true`이며 `GameMessageManager` 구독으로 `unlockOpType/unlockValue` 조건 만족 시 해금된다.
10. 잠긴 카탈로그의 상품은 `CanBuy/BuyAsync`에서 차단된다.
11. 구매 제한(`maxCount/remainCount`) 체크
12. `ShopProductBase.Price`(할인 반영 최종가)로 구매 가능 여부/통화 차감을 처리한다. 원가는 `ShopProductBase.PriceWithoutDiscount`를 사용한다.
13. 카탈로그 분기와 row -> product 변환 helper는 `ShopCatalogBase` 계층이 담당하며, ShopManager는 카탈로그 인덱싱/구매 검증/결제를 담당한다.
14. `ShopManager.onInitAwake()`는 catalog를 초기화하지 않으며, `Initialize()`가 유일한 manager 초기화 진입점이다.
15. `ShopCatalogBase`는 `Initialize() -> onInitialize() -> RefreshProducts() -> onRefresh()` 라이프사이클을 따른다. product 생성 책임은 `onRefresh()`에 있다.
16. 기본 `onRefresh()`는 `CHEST/PURCHASE/GOLD`의 테이블 전체 상품을 생성한다. `ShopCatalogDaily.onRefresh()`와 `ShopCatalogEvent.onRefresh()`가 고유 생성 로직을 override 한다.
17. `PURCHASE`는 `PurchaseManager.PurchaseAsync(internalProductId)` 위임, 그 외는 통화 차감/광고 시청 후 `RewardManager` 지급
18. 성공 시 `remainCount`와 daily 동적 상태를 저장 + SaveData 저장
19. ADS/FREE 상품 리필은 카탈로그 저장 버킷의 `adsRefreshUtcMs`로 별도 관리하며, ADS/FREE 구매 성공 시 `serverNow + 1day`로 기록한다.
20. 카탈로그 초기화/refresh 시 `adsRefreshUtcMs`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
21. refresh 조건 탐색은 `ShopManager.evaluateCatalogRefreshState(...)` 한 곳에서 처리한다.
22. 카탈로그 refresh 실행은 `ShopManager.tryRefreshCatalog(...)` 한 경로에서 처리한다. catalog-specific public API는 catalog instance가 호출하고, ShopManager에는 global executor와 post-commit만 둔다.
23. SaveData 로드는 `ShopStorage`에만 반영되고, 런타임 카탈로그 반영은 이후 `LoginManager`가 호출하는 `ShopManager.Initialize()`에서 수행한다.
24. `SHOP_DAILY`의 ADS/FREE 상품은 테이블에서 고정 로드하며 `dailyCatalogProducts`에는 저장하지 않는다. ADS/FREE의 `remainCount` 저장은 DAILY catalog bucket의 `productRemainCounts`를 사용한다.
25. 카탈로그별 남은 시간 조회는 `ShopCatalogBase.RemainAutoRefreshTimeMs`, `RemainAdsRefreshTimeMs`, `ShopCatalogDaily.RemainManualRefreshTimeMs`, `RemainManualRefreshCount`를 사용한다.
26. 로그인 초기화 마지막 단계에서 `LoginManager`가 `Initialize()`를 호출해 Shop 카탈로그 상태를 최종 정합화한다.
27. `GetCatalog<T>()`는 typed catalog 획득용이다. 사용 예: `ShopManager.Instance.GetCatalog<ShopCatalogDaily>()`.
28. `ShopCatalogBase.ResetAds()`와 `ShopCatalogDaily.RefreshByAdsAsync()`는 catalog instance public API다.
29. `ShopCatalogDaily.RefreshByAdsAsync()`는 daily catalog 내부에서 manual refresh 상태 판단, 광고 시청, 성공 기록을 직접 처리한다.
30. `ShopCatalogDaily.RefreshByAdsAsync()`는 rolling 24시간 기준 최대 5회만 허용한다. 상태는 `manualRefreshUtcMs`, `manualRefreshCount`로 관리한다.
31. `ShopCatalogDaily.RefreshByAdsAsync()` 제한 초과 시 `COMMON_ERROR_TYPE.SHOP_DAILY_MANUAL_REFRESH_COUNT_EXHAUSTED`를 반환한다.
32. `ShopManager`는 `syncCatalogRuntimeStates()`로 catalog별 runtime state 동기화를 공통 처리하고, catalog-specific manual refresh helper를 두지 않는다.
33. `ShopManager`의 catalog mutation 후처리는 product index 동기화와 local save queue로 제한한다.

---

## 3. Currency Rules

- `FREE`: 가격 0 구매로 사용(차감 없음)
- `ADS`: 광고 시청 성공 시 구매 성공
- `JEWEL`: `JEWEL_FREE` 우선 차감 후 부족분 `JEWEL_PAID` 차감
- `NO_ADS` 대여(`InventoryStorage.GetRentalRemainingMs("NO_ADS") > 0`) 중이면 `CanBuy`의 `AdsManager.CanShow` 체크와 `BuyAsync`의 광고 show를 skip한다.
- `SHOP_DISCOUNT_TYPE`이 설정된 상품은 `ShopProductBase.Price`(할인 반영 값)로 지갑 잔액 검증/차감을 수행한다.
- `BuyAsync`의 통화 상품 구매는 `InventoryManager.Storage.Wallet`에서 `CurrencyType` 기준으로 `Price`만큼 차감한다.
- `BuyAsync` 차감 경로에서 `Price < 0`은 즉시 차단하고 `SHOP_PRODUCT_PRICE_INVALID`를 반환한다. (음수 가격으로 재화 증감 금지)
- 통화 부족 시 `COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT`를 반환한다.
- `Initialize()` 이전의 `RefreshProducts/CanBuy/BuyAsync`와 catalog public API는 `COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED`로 실패한다.
- `ShopCatalogBase.ResetAds()`: 지정 카탈로그를 강제 refresh한다. (`forceCatalogRefresh=true`)
- `ShopCatalogDaily.RefreshByAdsAsync()`: DAILY 수동 refresh를 수행한다. 광고 실패는 `SHOP_ADS_SHOW_FAILED`, 횟수 소진은 `SHOP_DAILY_MANUAL_REFRESH_COUNT_EXHAUSTED`를 반환한다.
- `ShopCatalogDaily.RefreshByAdsAsync()`는 global refresh를 호출하지 않고 DAILY만 1회 refresh한다.
- SaveData 저장은 ShopManager 내부 초기화 루틴이 아니라 LoginManager 마지막 단계에서 수행한다.

---

## 4. Catalog Rules

- `DAILY`, `CHEST`, `GOLD`: `RewardManager` 지급
- `PURCHASE`: `PurchaseManager` 구매 처리 사용 (`BuyAsync` 보상 지급 로직 직접 사용 금지)
- `seasonId` 구매 제한(시즌 종료 임박 차단)은 ShopManager에서 검사한다.

---

## 5. Error Rules

- `CanBuy` 실패: `LastCanBuyErrorCode = SHOP_CAN_BUY_FAILED`
- `BuyAsync` 실패: 기본적으로 `COMMON_ERROR_TYPE.SHOP_BUY_FAILED`
- 단, 카탈로그 잠금 실패는 `COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT`을 반환한다.
- 단, 통화 부족 실패는 `COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT`를 그대로 반환한다.
- inner 실패 코드는 메시지에 `inner=...` 형태로 포함한다.

---

## 6. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopManager.cs`

---

## 7. Related

- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [22-inventory-system/12-inventory-wallet](../../22-inventory-system/12-inventory-wallet/SKILL.md)
