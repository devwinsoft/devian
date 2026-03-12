---
name: 12-shop-storage
description: ShopStorage를 통해 `shopId`별 구매 횟수와 전역 일일 리셋 상태를 저장/복원할 때 사용한다. 시간 기준은 서버 시간(RemoteConfig serverNowUtcMs)이다.
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
- `lastResetUtcDayStartMs` : 전역 일일 리셋 기준 시각(UTC day start)
- `purchaseCounts: Dictionary<string, int>`
- key: `shopId`
- value: 누적 구매 횟수(현재 리셋 주기 내)

---

## 3. Hard Rules

- 리셋 주기는 상품별이 아니라 ShopManager 전역 1일 고정이다.
- 시간 계산은 서버 시간(`RemoteConfigManager.TryGetServerNowUtcMs`) 기준만 사용한다.
- 구매 성공 시에만 `purchaseCounts[shopId]`를 증가시킨다.
- day start가 바뀌면 전체 카운트를 초기화한다.

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
