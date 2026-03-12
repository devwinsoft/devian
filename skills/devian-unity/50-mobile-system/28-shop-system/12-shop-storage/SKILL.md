---
name: 12-shop-storage
description: ShopStorage를 통해 `shopId`별 구매 횟수, 비광고 전역 일일 리셋 상태, FREE/ADS 카탈로그별 24시간 롤링 리셋 시작 시각을 저장/복원할 때 사용한다.
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

- `schemaVersion`
- `lastResetUtcDayStartMs` : 비광고 구매 제한 전역 일일 리셋 기준 시각(UTC day start)
- `purchaseCounts: Dictionary<string, int>`
- key: `shopId`
- value: 누적 구매 횟수(현재 리셋 주기 내)
- `adsCatalogResetStartedAtUtcMsByCatalog: Dictionary<string, long>`
- key: `SHOP_CATALOG_TYPE` 문자열
- value: 해당 카탈로그 FREE/ADS 구매 제한 24시간 롤링 리셋 시작 시각(UTC ms)

---

## 3. Hard Rules

- FREE/ADS + 구매 제한 상품은 카탈로그별 기준 시각 + 24시간 리셋을 사용한다.
- 비 ADS + 구매 제한 상품은 전역 1일(UTC day start) 리셋을 사용한다.
- 구매 성공 시에만 `purchaseCounts[shopId]`를 증가시킨다.
- FREE/ADS는 카탈로그별 `startedAtUtcMs` 기준 24시간 경과 시 카운트를 초기화한다.
- FREE/ADS 성공 시 `GetAdsResetRemainingMs(catalogType)`가 0이면 해당 시각으로 `startedAtUtcMs`를 갱신한다.
- 비 ADS는 day start가 바뀌면 전역 비 ADS 카운트를 초기화한다.
- FREE/ADS 리셋 남은 시간은 저장하지 않고 `startedAtUtcMs`와 현재 시각으로 계산한다.

---

## 4. SaveData

ShopStorage는 SaveData JSON의 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`
- legacy `purchaseLimits` 포맷은 `purchaseCounts`로 마이그레이션한다.

---

## 5. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopStorage.cs`

---

## 6. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
