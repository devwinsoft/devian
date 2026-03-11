---
name: 12-shop-storage
description: ShopStorage를 통해 SHOP_PRODUCT의 구매 제한(maxCount/resetDays) 상태를 저장/복원할 때 사용한다. 서버 기준 시간(RemoteConfig serverNowUtcMs)으로 기간을 계산한다.
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
- `purchaseLimits: Dictionary<string, ShopPurchaseLimitState>`
  - key: `productId`
  - value:
    - `periodStartUtcMs`
    - `purchaseCount`

---

## 3. Hard Rules

- 구매 제한 계산 시간 기준은 **서버 시간**(`RemoteConfigManager.TryGetServerNowUtcMs`)이다.
- 클라이언트 로컬 시간(`DateTime.Now`, `UtcNow`)으로 기간 판정하지 않는다.
- `maxCount == -1` 또는 `resetDays == -1`이면 제한 비활성이다.
- 제한 활성 시(`maxCount/resetDays >= 0`) 구매 성공 후에만 `purchaseCount`를 증가시킨다.

---

## 4. SaveData

ShopStorage는 SaveData JSON에 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`

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
