---
name: 10-shop-manager
description: MobileSystem ShopManager를 CompoSingleton으로 구현하고 catalog 기반 구매(`CanBuy(shopId)`, `BuyAsync(shopId)`) 플로우(할인 타입 반영 가격 계산, 통화 차감, ADS 시청, PurchaseManager 위임, `maxCount/remainCount` 제한, daily 동적 상태 복원)를 적용할 때 사용한다.
---

# 10-shop-manager

Status: ACTIVE
AppliesTo: v10

ShopManager는 MobileSystem 상점 구매 진입점이며, 카탈로그별 상품 목록을 관리한다.

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
public IReadOnlyList<ShopCatalogBase> GetCatalogs()
public ShopCatalogBase GetCatalog(SHOP_CATALOG_TYPE catalogType)
public CommonResult ResetAds(SHOP_CATALOG_TYPE catalogType)
public CommonResult<long> GetAdsResetRemainingMs(SHOP_CATALOG_TYPE catalogType)
public void RebuildCatalogProductsFromStorage()
```

핵심 플로우:

1. `shopId`로 `ShopProductBase` 조회
2. `SHOP_DAILY` 카탈로그는 초기화/리셋 시 `selectRate` 규칙으로 ADS/FREE 제외 대상에서 5개 선택 생성된다.
3. `SHOP_DAILY` 선택 생성에서 동일 `shopId`(pk)는 중복 선택하지 않는다.
4. 구매 제한 리셋 체크(서버 시간 기준): 구매 제한 상품은 카탈로그별 `autoRefreshUtcMsByCatalog`(다음 refresh 시각) + `autoRefreshDay` 규칙으로 처리한다.
5. 구매 제한(`maxCount/remainCount`) 체크
6. `ShopProductBase.Price`(할인 반영 최종가)로 구매 가능 여부/통화 차감을 처리한다. 원가는 `ShopProductBase.PriceWithoutDiscount`를 사용한다.
7. 카탈로그 분기는 `ShopCatalogBase` 계층이 담당하며, ShopManager는 카탈로그 인덱싱/구매 검증/결제를 담당한다.
8. `ShopCatalogBase`는 `Initialize()/onInitialize()` 라이프사이클을 따르며, ShopManager는 카탈로그를 사용할 때 `Initialize()` 완료 상태를 전제로 처리한다.
9. `PURCHASE`는 `PurchaseManager.PurchaseAsync(internalProductId)` 위임, 그 외는 통화 차감/광고 시청 후 `RewardManager` 지급
10. 성공 시 `remainCount`와 daily 동적 상태를 저장 + SaveData 저장
11. ADS/FREE 상품 리필은 `adsRefreshUtcMsByCatalog`로 별도 관리하며, ADS/FREE 구매 성공 시 `serverNow + 1day`로 기록한다.
12. 카탈로그 초기화/refresh 시 `adsRefreshUtcMsByCatalog`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
13. `GetAdsResetRemainingMs(catalogType)`으로 카탈로그별 리셋까지 남은 시간(ms)을 조회
14. SaveData 로드 후에는 `RebuildCatalogProductsFromStorage()`로 저장된 daily 상품 리스트(`shopId`, `discountType`, `remainCount`)를 런타임 카탈로그에 복원한다. 이 리스트는 ADS/FREE 제외 5개 동적 상품만 저장한다.
15. `SHOP_DAILY`의 ADS/FREE 상품은 테이블에서 고정 로드하며 `dailyCatalogProducts`에는 저장하지 않는다. ADS/FREE의 `remainCount` 저장은 `productRemainCounts`를 사용한다.
16. 로그인 초기화 마지막 단계에서 `LoginManager`가 `Initialize()`를 호출해 Shop 카탈로그 상태를 최종 정합화한다.
17. `SHOP_DAILY` 카탈로그 타이머 만료 시 `dailyCatalogProducts`를 비우고 카탈로그를 rebuild하여 ADS/FREE 제외 5개 선택 생성을 다시 수행한다.
18. `Initialize()`는 초기 조건(카탈로그 미구성/DAILY 저장 상태 불일치) 또는 카탈로그 리셋 조건(`autoRefreshUtcMsByCatalog` 만료)일 때에만 전체 카탈로그 product list를 rebuild한다.

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
- `ResetAds(catalogType)`: 지정 카탈로그의 구매 제한 카운트/`remainCount`를 즉시 초기화한다.
- `GetAdsResetRemainingMs(catalogType)`: 지정 카탈로그 구매 제한 리셋까지 남은 시간(ms)을 반환한다.
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
- [14-shop-factory](../14-shop-factory/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [22-inventory-system/12-inventory-wallet](../../22-inventory-system/12-inventory-wallet/SKILL.md)
