---
name: 12-shop-storage
description: ShopStorage에서 `remainCount` 중심 상태(카탈로그별 `autoRefreshDay` 자동 갱신, DAILY 동적 상품 상태)를 저장/복원할 때 사용한다.
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

- `schemaVersion` (현재 `8`)
- `productRemainCounts: Dictionary<string, int>`
- key: `shopId`
- value: 현재 남은 구매 가능 횟수(`remainCount`)
- `autoRefreshUtcMsByCatalog: Dictionary<string, long>`
- key: `SHOP_CATALOG_TYPE` 문자열
- value: 해당 카탈로그의 다음 자동 갱신 시각(UTC ms)
- `adsRefreshUtcMsByCatalog: Dictionary<string, long>`
- key: `SHOP_CATALOG_TYPE` 문자열
- value: 해당 카탈로그 ADS/FREE 상품의 다음 리필 시각(UTC ms)
- `dailyCatalogProducts: List<ShopDailyProductState>`
- `ShopDailyProductState = { shopId, discountType, remainCount }`
- `dailyCatalogProducts`는 `SHOP_DAILY`의 ADS/FREE 제외 동적 5개 상품만 저장한다.
- legacy migration buffer:
- `_legacyPurchaseCounts: Dictionary<string, int>` (직렬화 안 함, 구버전 데이터 마이그레이션용)

---

## 3. Hard Rules

- 구매 제한 상품은 카탈로그별 `autoRefreshDay` 주기 리셋을 사용한다.
- 구매 성공 시 `remainCount`를 갱신한다.
- 무제한 상품(`maxCount=-1`)은 `productRemainCounts`에 저장하지 않는다.
- DAILY 카탈로그 동적 결과(`shopId`, `discountType`, `remainCount`)는 ADS/FREE 제외 5개 상품만 `dailyCatalogProducts`로 저장/복원한다.
- DAILY 카탈로그 ADS/FREE 상품은 `dailyCatalogProducts`에 저장하지 않는다. 구매 제한 수량 저장이 필요하면 `productRemainCounts`를 사용한다.
- `autoRefreshUtcMsByCatalog`는 시작 시각이 아니라 다음 refresh 시각으로 저장한다.
- `adsRefreshUtcMsByCatalog`는 ADS/FREE 구매 성공 시 `serverNow + 1day`로 기록한다.
- 카탈로그 초기화/refresh 시 `adsRefreshUtcMsByCatalog`가 없거나 만료면 ADS/FREE 제한 상품을 `remainCount=maxCount`로 리필한다.
- `purchaseCounts` 기반 저장은 사용하지 않는다.

---

## 4. SaveData

ShopStorage는 SaveData JSON의 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`
- legacy `purchaseCounts`/`purchaseLimits`는 `_legacyPurchaseCounts`로 마이그레이션한다.
- 런타임 카탈로그 동기화 시 legacy count를 `remainCount`로 1회 변환한다.
- 최신 스키마는 `schemaVersion=8`이다.

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
