---
name: 16-shop-catalog-chest
description: `ShopCatalogChest`의 레벨/경험치/레벨업과 `SHOP_CATALOG_CHEST`, `SHOP_PRODUCT_CHEST_TYPE` 기반 chest 구매 보상 규칙을 구현할 때 사용한다.
---

# 16-shop-catalog-chest

Status: ACTIVE
AppliesTo: v10
Type: Design / Runtime SSOT

`ShopCatalogChest`는 더 이상 `SHOP_CHEST.rewardGroupId`를 직접 소비하는 단순 보상 카탈로그가 아니다.
이 문서는 chest catalog progression(level/exp)과 purchase reward routing을 정의한다.

---

## 1. Current Gap

- `ShopCatalogChest`는 현재 `ShopCatalogBase` thin wrapper다.
- `SHOP_CHEST`는 현재 `RewardGroupId`, `Amount`를 직접 가진다.
- `ShopProductFactory.CreateChestProduct()`는 chest row를 일반 `ShopRewardProductBase`로 만든다.
- `ShopManager.validateShopProductConfig()` / `buyRewardCatalogAsync()`는 `rewardGroupId` non-empty를 전제로 한다.
- `ShopCatalogChestStorageData`는 `adsRefreshUtcMs`, `productRemainCounts`만 저장한다.

따라서 이번 변경은 table 추가만이 아니라 chest 전용 purchase flow 분리가 필요하다.

---

## 2. New Table / Enum Contract

### Enum

입력: `input/Domains/Game/ENUM_META.json`

```json
SHOP_PRODUCT_CHEST_TYPE: [NONE, ADS, ONE, TEN]
```

- 요청 문구의 `TES` 표기는 오타로 보고 `TEN`만 사용한다.

### SHOP_CHEST

입력: `input/Domains/Game/ShopTable.xlsx`

- 유지:
  - `shopId`
  - `nameId`
  - `currencyType`
  - `price`
  - `amount`
  - `maxCount`
- 추가:
  - `productChestType` (`SHOP_PRODUCT_CHEST_TYPE`)
- 삭제:
  - `rewardGroupId`
- 규칙:
  - `ADS` product는 `currencyType=ADS`, `price=0` 유지
  - `ONE/TEN`은 유료 또는 재화 결제 chest 상품이다.
  - `amount`는 reward 반복 지급 횟수다. 예를 들어 `amount=10`이면 선택된 reward group을 `10`회 적용한 것과 동일한 보상을 준다.
  - `amount`는 exp 획득량에는 곱하지 않는다. exp는 `SHOP_CATALOG_CHEST`의 `AdsExp/GainExp01/GainExp10`만 사용한다.

### SHOP_CATALOG_CHEST

입력: `input/Domains/Game/ShopTable.xlsx`

권장 row shape:

- `Level` (`int`, pk, 1-base)
- `AdsExp` (`int`)
- `GainExp01` (`int`)
- `GainExp10` (`int`)
- `MaxExp` (`int`)
- `RewardAds` (`string`, rewardGroupId)
- `RewardPaid01` (`string`, rewardGroupId)
- `RewardPaid10` (`string`, rewardGroupId)

Hard rules:

- chest progression row 조회 키는 현재 `Level`이다.
- `TB_SHOP_CATALOG_CHEST.Get(level)`로 현재 레벨 row를 읽을 수 있어야 한다.
- current row가 없으면 chest progression 설정 오류로 본다.
- max level은 `SHOP_CATALOG_CHEST` table에 존재하는 최대 `Level` 값이다.

---

## 3. Runtime Contract

`ShopCatalogChest` public contract:

```csharp
public int Level { get; }
public int CurrentExp { get; }
public int MaxExp { get; }
public void LevelUp();
```

Rules:

- `Level` 기본값은 `1`
- `CurrentExp` 기본값은 `0`
- `MaxExp`는 현재 `SHOP_CATALOG_CHEST.Level` row의 `maxExp`를 계산해서 노출한다. 별도 저장하지 않는다.
- `CurrentExp`는 누적 lifetime exp가 아니라 현재 레벨 구간 exp다.
- `LevelUp()`는 level을 증가시키고 overflow exp를 다음 레벨로 이월한다.
- 권장 구현은 `while (CurrentExp >= MaxExp && nextRow exists)`로 overflow exp를 보존하는 것이다.
- 다음 level row가 없으면 최고 레벨로 clamp해야 한다.
- 최고 레벨에서는 경험치를 획득하지 않는다.
- 최고 레벨에서는 `CurrentExp=0`으로 고정한다.
- 최고 레벨에서는 `MaxExp=0`으로 노출하는 것을 권장한다. 진행 필요 exp가 더 이상 없음을 런타임 값에서 명확히 표현할 수 있다.

---

## 4. Purchase Flow Rules

Chest purchase reward source는 `SHOP_CHEST` row가 아니라 현재 chest level row다.

구매 성공 흐름:

1. `ShopCatalogChest` instance를 얻는다.
2. 현재 `Level`의 `SHOP_CATALOG_CHEST` row를 조회한다.
3. `SHOP_PRODUCT_CHEST_TYPE`으로 reward / exp를 결정한다.
4. 광고 시청 또는 재화 차감을 수행한다.
5. 결정된 reward group을 적용한다.
6. 선택된 reward group을 `SHOP_CHEST.amount` 횟수만큼 적용한다.
7. 결정된 exp를 chest catalog에 적립한다.
8. 현재 row의 `MaxExp`를 넘기면 자동 레벨업 한다.
9. `remainCount`, `adsRefreshUtcMs`, save state를 반영한다.

매핑:

- `ADS` -> reward=`RewardAds`, exp=`AdsExp`
- `ONE` -> reward=`RewardPaid01`, exp=`GainExp01`
- `TEN` -> reward=`RewardPaid10`, exp=`GainExp10`

Hard rules:

- reward row 선택은 exp 적립/레벨업 이전에 해야 한다. 같은 구매가 중간에 다음 레벨 reward로 바뀌면 안 된다.
- reward 지급 multiplier는 항상 `SHOP_CHEST.amount`를 사용한다.
- reward multiplier는 현재처럼 reward group apply 반복 또는 동등한 multiplier API로 처리한다.
- auto level-up threshold는 현재 level row의 `MaxExp`다.
- 최고 레벨에서는 exp를 적립하지 않는다.
- 최고 레벨에서는 구매 후에도 `CurrentExp=0`을 유지한다.
- chest purchase success는 save 전에 `level/currentExp`를 storage에 반영해야 한다.
- 기존 ADS refill semantics(`adsRefreshUtcMs`)와 limited purchase remain semantics(`productRemainCounts`)는 유지한다.

---

## 5. Product Model Impact

현재 generic reward product model은 chest progression에 맞지 않는다.

구현은 최소한 아래 둘 중 하나를 처리해야 한다.

- 권장: `ShopProductChest : ShopProductBase` 추가, `SHOP_PRODUCT_CHEST_TYPE ChestType`, `int Amount` 보유
- 차선: chest도 기존 reward product를 유지하되, `CHEST` catalog에 한해 빈 `rewardGroupId`를 허용하고 runtime에서 reward를 동적으로 조회

권장 이유:

- 현재 validation은 non-empty `RewardGroupId`를 강제한다.
- 현재 buy path는 `product.RewardGroupId`를 직접 적용한다.
- chest reward source는 이제 static product row가 아니라 catalog state다.

---

## 6. Storage / SaveData

`ShopCatalogChestStorageData` 소유 상태:

- `adsRefreshUtcMs`
- `productRemainCounts`
- `level`
- `currentExp`

Rules:

- `ShopStorage.schemaVersion`는 증가해야 한다. (`11 -> 12` 예상)
- 구세이브에 chest progression 필드가 없으면 `level=1`, `currentExp=0`으로 복원한다.
- `MaxExp`는 table 기반 계산값이므로 serialize하지 않는다.
- JSON codec은 `catalogs.CHEST.level`, `catalogs.CHEST.currentExp`를 저장/복원해야 한다.
- 저장 복원 시 `level`이 최대 레벨 이상이면 최대 레벨로 clamp하고 `currentExp=0`으로 정규화한다.

---

## 7. Affected Files

Data / codegen:

- `input/Domains/Game/ENUM_META.json`
- `input/Domains/Game/ShopTable.xlsx`
- generated domain outputs for `SHOP_CHEST`, `SHOP_CATALOG_CHEST`, `SHOP_PRODUCT_CHEST_TYPE`

Runtime (3-path mirror):

- `Runtime/Shop/Catalog/ShopCatalogChest.cs`
- `Runtime/Shop/Catalog/ShopCatalogBase.cs` (공통 helper를 올릴 경우)
- `Runtime/Shop/ShopProduct.cs`
- `Runtime/Shop/ShopProductFactory.cs`
- `Runtime/Shop/ShopManager.cs`
- `Runtime/Shop/ShopStorage.cs`
- `Runtime/SaveData/JsonCodec/SaveDataJsonCodecShop.cs`

Sample table data:

- `framework-cs/apps/UnityExample/Assets/Bundles/Tables/ndjson/SHOP_CHEST.json`
- `framework-cs/apps/UnityExample/Assets/Bundles/Tables/ndjson/SHOP_CATALOG_CHEST.json`

---

## 8. Recommended Implementation Order

1. `ShopTable.xlsx`에서 `SHOP_PRODUCT_CHEST_TYPE`, `SHOP_CATALOG_CHEST`, 변경된 `SHOP_CHEST` 스키마를 authoring한다.
2. domain enum/table codegen을 다시 돌린다.
3. chest storage + save codec에 `level/currentExp`를 추가한다.
4. chest product model이 `ChestType`, `Amount`를 알 수 있도록 `ShopProduct` / `ShopProductFactory`를 정리한다.
5. `ShopCatalogChest`에 current row lookup, `Level`, `CurrentExp`, `MaxExp`, `LevelUp()`, max-level clamp를 구현한다.
6. `ShopManager`의 chest buy path를 generic reward product path와 분리하고 reward multiplier=`amount`를 반영한다.
7. ADS refill, remainCount, old save default, max-level exp ignore를 검증한다.
8. 관련 shop 문서를 갱신한다.

---

## 9. Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-shop-product](../11-shop-product/SKILL.md)
- [12-shop-storage](../12-shop-storage/SKILL.md)
- [13-shop-catalog](../13-shop-catalog/SKILL.md)
- [14-shop-catalog-factory](../14-shop-catalog-factory/SKILL.md)
