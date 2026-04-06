---
name: 12-inventory-wallet
description: Legacy note. Inventory currency state is flattened into InventoryStorage and the InventoryWallet wrapper is removed.
---

# 12-inventory-wallet

Status: ACTIVE
AppliesTo: v10

이 문서는 legacy 이름을 유지하지만, 현재 정본 구조는 `InventoryWallet`를 사용하지 않는다.
통화 상태는 `InventoryStorage` 내부 `Dictionary<CURRENCY_TYPE, long>`로 평탄화되었다.

---

## 1. Current Rule

- `InventoryWallet` 클래스는 제거한다.
- currency 잔고는 `InventoryStorage`가 직접 소유한다.
- currency helper는 `InventoryStorage`에 둔다.
  - `GetCurrencyAmount(CURRENCY_TYPE)`
  - `TryAddCurrency(CURRENCY_TYPE, long)`
  - `EnumerateCurrencyBalancesForSave()`

`CURRENCY_TYPE.JEWEL`은 파생값이며 `JEWEL_FREE + JEWEL_PAID`로 계산한다.

---

## 2. InventoryStorage 연계

InventoryStorage는 wallet wrapper 대신 currency dictionary를 직접 가진다.

```csharp
readonly Dictionary<CURRENCY_TYPE, long> mCurrencyBalances = new();
```

- Currency 연산(`GetCurrencyAmount/TryAddCurrency`)은 `InventoryStorage`가 직접 제공한다.
- `CURRENCY_TYPE.JEWEL` 직접 add/set은 금지한다.

---

## 3. JEWEL 예외 규칙

- `CURRENCY_TYPE.JEWEL`은 표시/조회용 aggregate다.
- 저장 대상은 `JEWEL_FREE`, `JEWEL_PAID`이며 `JEWEL`은 저장하지 않는다.
- 보상/차감 입력에서 `JEWEL`이 직접 들어오면 invalid로 처리한다.

---

## 4. SaveData JSON

Inventory currency 직렬화는 `InventoryStorage.EnumerateCurrencyBalancesForSave()`를 사용한다.

- serialize: `JEWEL` 제외
- deserialize: `JEWEL` 키가 오면 skip

---

## 5. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Inventory/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/MobilePackage/Runtime/Inventory/`

---

## 6. Related

- [11-inventory-storage](../11-inventory-storage/SKILL.md)
- [10-inventory-manager](../10-inventory-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
