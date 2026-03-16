---
name: 12-shop-storage
description: ShopStorage에서 catalog별 typed storage data를 저장/복원할 때 사용한다.
---

# 12-shop-storage

Status: ACTIVE
AppliesTo: v10

ShopStorage는 Shop catalog runtime 상태의 저장 컨테이너다.

---

## 1. Class

```csharp
[Serializable]
public sealed class ShopStorage
```

- namespace: `Devian`
- asmdef: `Devian.Samples.MobilePackage`
- 소유자: `ShopManager`

---

## 2. Typed Storage Data

```csharp
[Serializable]
public abstract class ShopCatalogStorageDataBase
{
    public abstract SHOP_CATALOG_TYPE CatalogType { get; }
}
```

```csharp
[Serializable]
public abstract class ShopCatalogProductRemainStorageDataBase : ShopCatalogStorageDataBase
{
    public Dictionary<string, int> productRemainCounts = new();
}
```

```csharp
public sealed class ShopCatalogDailyStorageData : ShopCatalogProductRemainStorageDataBase
{
    public long autoRefreshUtcMs;
    public long adsRefreshUtcMs;
    public long manualRefreshUtcMs;
    public int manualRefreshRemainCount;
    public List<ShopDailyProductState> dailyCatalogProducts = new();
}

public sealed class ShopCatalogChestStorageData : ShopCatalogProductRemainStorageDataBase
{
    public long adsRefreshUtcMs;
    public int level;
    public int currentExp;
}

public sealed class ShopCatalogPurchaseStorageData : ShopCatalogStorageDataBase
{
}

public sealed class ShopCatalogGoldStorageData : ShopCatalogProductRemainStorageDataBase
{
    public long adsRefreshUtcMs;
}

public sealed class ShopCatalogEventStorageData : ShopCatalogStorageDataBase
{
    public long autoRefreshUtcMs;
}
```

`ShopStorage` 필드:

- `schemaVersion` (현재 `12`)
- `daily: ShopCatalogDailyStorageData`
- `chest: ShopCatalogChestStorageData`
- `purchase: ShopCatalogPurchaseStorageData`
- `gold: ShopCatalogGoldStorageData`
- `eventCatalog: ShopCatalogEventStorageData`
- legacy migration buffer:
  - `_legacyPurchaseCounts: Dictionary<string, int>` (직렬화 안 함)

보조 타입:

- `ShopDailyProductState = { shopId, discountType, remainCount }`

---

## 3. Catalog Data Ownership

- `DAILY`
  - `autoRefreshUtcMs`
  - `adsRefreshUtcMs`
  - `manualRefreshUtcMs`
  - `manualRefreshRemainCount`
  - `productRemainCounts`
  - `dailyCatalogProducts`
- `CHEST`
  - `adsRefreshUtcMs`
  - `productRemainCounts`
  - `level`
  - `currentExp`
- `GOLD`
  - `adsRefreshUtcMs`
  - `productRemainCounts`
- `PURCHASE`
  - 현재 catalog 전용 저장 상태 없음
- `EVENT`
  - `autoRefreshUtcMs`

규칙:

- `dailyCatalogProducts`는 `SHOP_DAILY`의 ADS/FREE 제외 동적 5개 상품만 저장한다.
- `DAILY`의 ADS/FREE 상품은 `dailyCatalogProducts`에 저장하지 않는다. 해당 상품의 `remainCount`는 `daily.productRemainCounts`를 사용한다.
- 무제한 상품(`maxCount=-1`)은 `productRemainCounts`에 저장하지 않는다.
- `PURCHASE`와 `EVENT`에 필요 없는 상태를 미리 넣지 않는다.
- 최고 레벨 상태는 `currentExp=0`으로 저장/복원한다.

---

## 4. Hard Rules

- 저장 구조는 generic dictionary가 아니라 catalog별 typed storage data field를 사용한다.
- `ShopCatalogFactory.CreateRuntimeCatalogs(storage)`는 catalog 생성 시 해당 catalog type의 storage data를 같이 전달한다.
- 각 catalog는 자기 storage data만 해석한다.
- non-daily catalog는 table product 생성 후 자기 storage data의 `productRemainCounts`를 직접 적용한다.
- `DAILY`는 storage의 `dailyCatalogProducts`가 valid하면 그 상태로 동적 5개를 복원하고, invalid/empty면 table에서 새로 선택 생성한다.
- `autoRefreshUtcMs`는 시작 시각이 아니라 다음 refresh 시각이다.
- `EVENT.autoRefreshUtcMs`는 주기값이 아니라 다음 `startTime/endTime` 경계 시각이다.
- `adsRefreshUtcMs`는 ADS/FREE 구매 성공 시 `serverNow + 1day`로 기록한다.
- `CHEST.level` 기본값은 `1`이다.
- `CHEST.currentExp` 기본값은 `0`이다.
- `CHEST`가 최대 레벨이면 `currentExp=0`으로 정규화한다.
- `manualRefreshUtcMs`는 `ShopCatalogDaily.RefreshByAdsAsync()` 성공 시 `serverNow + 1day`로 기록한다.
- `manualRefreshRemainCount`는 rolling 24시간 남은 횟수다.
- 초기 상태와 만료 후 reset 상태의 `manualRefreshRemainCount`는 `5`다.
- 수동 refresh 성공 시 `manualRefreshRemainCount`를 1 감소시킨다.
- legacy `purchaseCounts`/`purchaseLimits`는 `_legacyPurchaseCounts`로만 1회 마이그레이션한다.

---

## 5. SaveData

ShopStorage는 SaveData JSON의 `shop` 섹션으로 직렬화한다.

- serialize: `SaveDataJsonCodecShop.Serialize(ShopStorage)`
- deserialize: `SaveDataJsonCodecShop.DeserializeInto(JObject, ShopStorage)`
- 최신 스키마는 `schemaVersion=12`
- 저장 JSON은 `catalogs` 하위에 catalog 단위로 묶어 저장한다.
- JSON shape는 grouped catalog 구조를 유지하지만, 런타임 메모리 구조는 typed storage field를 사용한다.
- `DAILY`는 `adsRefreshUtcMs`, `autoRefreshUtcMs`, `manualRefreshUtcMs`, `manualRefreshRemainCount`, `productRemainCounts`, `dailyCatalogProducts`를 저장한다.
- `CHEST`는 `adsRefreshUtcMs`, `productRemainCounts`, `level`, `currentExp`를 저장한다.
- `GOLD`는 `adsRefreshUtcMs`, `productRemainCounts`를 저장한다.
- `EVENT`는 `autoRefreshUtcMs`를 저장한다.
- `PURCHASE`는 현재 직렬화할 catalog 전용 상태가 없다.

---

## 6. Runtime Usage

- `ShopManager.Initialize()`는 SaveData에서 복원된 `ShopStorage`를 읽어 runtime catalog를 생성한다.
- `ShopCatalogFactory`가 `ShopStorage.GetCatalogData(...)`로 catalog-specific storage data를 주입한다.
- `ShopManager.synchronizeProductIndexFromCatalogs()`는 product index rebuild만 한다. storage 복원 책임은 없다.
- runtime cache invalidation이 필요할 때는 `ShopManager.InvalidateRuntimeState()`를 호출한다.

---

## 7. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Shop/ShopStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Shop/ShopStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Shop/ShopStorage.cs`

---

## 8. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
