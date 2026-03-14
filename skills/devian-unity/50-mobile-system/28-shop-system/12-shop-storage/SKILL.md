---
name: 12-shop-storage
description: ShopStorage에서 `remainCount` 중심 상태(카탈로그별 refresh 시각, DAILY 동적 상품 상태)를 저장/복원할 때 사용한다.
---

# 12-shop-storage

Status: ACTIVE
AppliesTo: v10

ShopStorage는 ShopManager의 구매 제한 상태 컨테이너다.

---

## 1. Class

```csharp
[Serializable]
public sealed class ShopStorage
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobileSystem`
- 소유자: `ShopManager`

---

## 2. State

- `schemaVersion` (현재 `10`)
- `catalogs: Dictionary<string, ShopCatalogStorageState>`
- key: `SHOP_CATALOG_TYPE` 문자열 (`DAILY/CHEST/PURCHASE/GOLD/EVENT`)
- `ShopCatalogStorageState`
- `autoRefreshUtcMs: long` (카탈로그 다음 자동 갱신 시각 UTC ms)
- `adsRefreshUtcMs: long` (카탈로그 ADS/FREE 다음 리필 시각 UTC ms)
- `manualRefreshUtcMs: long` (`DAILY` 수동 refresh rolling 24시간 만료 시각 UTC ms)
- `manualRefreshCount: int` (`DAILY` 수동 refresh rolling 24시간 사용 횟수)
- `productRemainCounts: Dictionary<string, int>` (`shopId -> remainCount`)
- `dailyCatalogProducts: List<ShopDailyProductState>` (DAILY에서만 사용)
- `ShopDailyProductState = { shopId, discountType, remainCount }`
- `dailyCatalogProducts`는 `SHOP_DAILY`의 ADS/FREE 제외 동적 5개 상품만 저장한다.
- legacy migration buffer:
- `_legacyPurchaseCounts: Dictionary<string, int>` (직렬화 안 함, 구버전 데이터 마이그레이션용)

---

## 3. Hard Rules

- 일반 카탈로그의 구매 제한 자동 refresh는 카탈로그별 `autoRefreshDays` 주기를 사용한다. (`autoRefreshDays <= 0`이면 자동 refresh 미사용)
- `EVENT`의 `autoRefreshUtcMs`는 주기값이 아니라 다음 `startTime/endTime` 경계 시각을 저장한다.
- 구매 성공 시 `remainCount`를 갱신한다.
- 무제한 상품(`maxCount=-1`)은 해당 catalog bucket의 `productRemainCounts`에 저장하지 않는다.
- DAILY 카탈로그 동적 결과(`shopId`, `discountType`, `remainCount`)는 ADS/FREE 제외 5개 상품만 `dailyCatalogProducts`로 저장/복원한다.
- DAILY 카탈로그 ADS/FREE 상품은 `dailyCatalogProducts`에 저장하지 않는다. 구매 제한 수량 저장이 필요하면 DAILY bucket의 `productRemainCounts`를 사용한다.
- `autoRefreshUtcMs`는 시작 시각이 아니라 다음 refresh 시각으로 저장한다.
- `EVENT`는 `dailyCatalogProducts` 같은 별도 동적 payload를 저장하지 않는다.
- `adsRefreshUtcMs`는 ADS/FREE 구매 성공 시 `serverNow + 1day`로 기록한다.
- 카탈로그 초기화/refresh 시 `adsRefreshUtcMs`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
- `manualRefreshUtcMs`는 `ShopCatalogDaily.RefreshByAdsAsync()` 성공 시 `serverNow + 1day`로 기록한다. 만료 전 재사용은 시각을 밀지 않는다.
- `manualRefreshCount`는 `ShopCatalogDaily.RefreshByAdsAsync()` 성공 횟수이며, 만료 시 0으로 초기화한다.
- `manualRefreshUtcMs/manualRefreshCount`의 만료 판정과 runtime 반영은 `ShopCatalogDaily.SyncRuntimeState(...)`가 담당한다.
- `purchaseCounts` 기반 저장은 사용하지 않는다.

---

## 4. SaveData

ShopStorage는 SaveData JSON의 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`
- legacy `purchaseCounts`/`purchaseLimits`는 `_legacyPurchaseCounts`로 마이그레이션한다.
- 런타임 카탈로그 초기화 시 각 catalog가 자기 bucket의 remain 상태를 직접 복원한다. non-daily는 table product 생성 후 적용하고, DAILY는 `dailyCatalogProducts`로 직접 복원한다.
- legacy count는 catalog 초기화 시 `remainCount`로 1회 변환한다.
- 최신 스키마는 `schemaVersion=10`이다.
- 저장 JSON은 flat key 대신 `catalogs` 하위에 catalog 단위로 묶어 저장한다.

---

## 5. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopStorage.cs`

---

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
