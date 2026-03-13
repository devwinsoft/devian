---
name: 12-shop-storage
description: ShopStorage에서 `remainCount` 중심 상태(카탈로그별 24시간 리셋, DAILY 동적 상품 상태)를 저장/복원할 때 사용한다.
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

- `schemaVersion` (현재 `6`)
- `productRemainCounts: Dictionary<string, int>`
- key: `shopId`
- value: 현재 남은 구매 가능 횟수(`remainCount`)
- `adsCatalogResetStartedAtUtcMsByCatalog: Dictionary<string, long>`
- key: `SHOP_CATALOG_TYPE` 문자열
- value: 해당 카탈로그 구매 제한 24시간 롤링 리셋 시작 시각(UTC ms)
- `dailyCatalogProducts: List<ShopDailyProductState>`
- `ShopDailyProductState = { shopId, discountType, remainCount }`
- legacy migration buffer:
- `_legacyPurchaseCounts: Dictionary<string, int>` (직렬화 안 함, 구버전 데이터 마이그레이션용)

---

## 3. Hard Rules

- 구매 제한 상품은 카탈로그별 기준 시각 + 24시간 리셋을 사용한다.
- 구매 성공 시 `remainCount`를 갱신한다.
- 무제한 상품(`maxCount=-1`)은 `productRemainCounts`에 저장하지 않는다.
- DAILY 카탈로그 동적 결과(`shopId`, `discountType`, `remainCount`)는 `dailyCatalogProducts`로 저장/복원한다.
- `purchaseCounts` 기반 저장은 사용하지 않는다.

---

## 4. SaveData

ShopStorage는 SaveData JSON의 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`
- legacy `purchaseCounts`/`purchaseLimits`는 `_legacyPurchaseCounts`로 마이그레이션한다.
- 런타임 카탈로그 동기화 시 legacy count를 `remainCount`로 1회 변환한다.
- 최신 스키마는 `schemaVersion=6`이다.

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
