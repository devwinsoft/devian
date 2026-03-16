---
name: 12-inventory-wallet
description: InventoryStorage의 Wallet을 InventoryWallet 클래스로 전환한다. Dictionary 기반 단일 구현으로 통화 연산 중복을 제거하고, JEWEL은 JEWEL_FREE+JEWEL_PAID 파생 조회값으로 처리하며 직접 set/add를 막아야 할 때 사용한다.
---

# 12-inventory-wallet

Status: ACTIVE
AppliesTo: v10

InventoryWallet는 InventoryStorage의 통화 컨테이너다.
`Dictionary<CURRENCY_TYPE, long>`를 내부 저장소로 사용하고 통화 연산을 단일 지점으로 관리한다.

---

## 1. InventoryWallet

`InventoryWallet`는 아래 API를 제공한다.

- `Get(CURRENCY_TYPE)` — 잔고 조회
- `TryAdd(CURRENCY_TYPE, long)` — 증감 적용
- `EnumerateForSave()` — 저장용 열거
- `Clear()` — 전체 초기화

`CURRENCY_TYPE.JEWEL`은 파생값이며 `Get(JEWEL_FREE) + Get(JEWEL_PAID)`로 계산한다.

---

## 2. InventoryStorage 연계

InventoryStorage는 아래 형태로 Wallet을 소유한다.

```csharp
readonly InventoryWallet mWallet = new();
public InventoryWallet Wallet => mWallet;
```

- Currency 연산(`Get/TryAdd`)은 `InventoryStorage`가 중복 구현하지 않고 `InventoryWallet` API를 직접 사용한다.
- `CURRENCY_TYPE.JEWEL` 직접 add/set은 금지한다.

---

## 3. JEWEL 예외 규칙

- `CURRENCY_TYPE.JEWEL`은 표시/조회용 aggregate다.
- 저장 대상은 `JEWEL_FREE`, `JEWEL_PAID`이며 `JEWEL`은 저장하지 않는다.
- 보상/차감 입력에서 `JEWEL`이 직접 들어오면 invalid로 처리한다.

---

## 4. SaveData JSON

Inventory wallet 직렬화는 wallet 클래스의 저장 전용 열거를 사용한다.

- serialize: `JEWEL` 제외
- deserialize: `JEWEL` 키가 오면 skip

---

## 5. Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Inventory/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Inventory/`

---

## 6. Related

- [11-inventory-storage](../11-inventory-storage/SKILL.md)
- [10-inventory-manager](../10-inventory-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
