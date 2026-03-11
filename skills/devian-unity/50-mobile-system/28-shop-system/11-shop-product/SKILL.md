---
name: 11-shop-product
description: MetaTable의 SHOP_PRODUCT 테이블(TB_SHOP_PRODUCT)을 ShopProduct 클래스로 래핑하고 productId 조회/검증/필드 표준화(rewardGroupId)할 때 사용한다.
---

# 11-shop-product

Status: ACTIVE
AppliesTo: v10

ShopProduct는 `TB_SHOP_PRODUCT` row를 런타임에서 안전하게 사용하는 래퍼다.

---

## 1. Source Table

- 입력 테이블: `input/Domains/Game/MetaTable.xlsx` 시트 `SHOP_PRODUCT`
- 필드:
  - `productId` (pk)
  - `nameId`
  - `currencyType`
  - `price`
  - `rewardGroupId`
  - `amount`
  - `maxCount`
  - `resetDays`

주의:
- 컨벤션은 `rewardGroupId`가 정본이다.
- `RewardId` 이름 사용 금지.

---

## 2. Class Contract

```csharp
public sealed class ShopProduct
{
    public string ProductId { get; }
    public string NameId { get; }
    public CURRENCY_TYPE CurrencyType { get; }
    public int Price { get; }
    public string RewardGroupId { get; }
    public int Amount { get; }
    public int MaxCount { get; }
    public int ResetDays { get; }

    public static ShopProduct Get(string productId);
    public static bool TryGet(string productId, out ShopProduct product);
}
```

- 내부적으로 `TB_SHOP_PRODUCT.Get(productId)`를 사용한다.
- row가 없으면 null/false를 반환한다.

---

## 3. Hard Rules

- 필드명은 `RewardGroupId`로 유지한다 (`RewardId` 금지).
- string 필드는 null 방어(`?? string.Empty`)를 적용한다.
- ShopManager 외부에서 테이블 row를 직접 다루지 않도록 래퍼를 우선 사용한다.

---

## 4. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopProduct.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Shop/ShopProduct.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Shop/ShopProduct.cs`

---

## 5. Related

- [10-shop-manager](../10-shop-manager/SKILL.md)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
