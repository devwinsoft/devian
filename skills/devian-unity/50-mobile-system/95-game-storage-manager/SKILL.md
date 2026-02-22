# 50-mobile-system / 95-game-storage-manager — GameStorageManager


Status: ACTIVE
AppliesTo: v10
Type: Design / SSOT


## Purpose

게임 저장 파일(JSON 형식)의 **통합 직렬화/역직렬화**를 담당하는 상위 저장 컨테이너.
InventoryStorage와 PurchaseStorage(최소 구매 상태 스냅샷)를 포함하여, 향후 다른 게임 데이터(미션 진행도, 플레이어 프로필 등)도 포괄한다.


---


## Background

기존에는 `InventoryStorage.ToJson()` / `InventoryStorage.FromJson()`이 직렬화를 직접 담당했다.
이 책임을 **GameStorageManager**로 이전하여:

1. InventoryStorage는 데이터 관리(CRUD)에만 집중
2. GameStorageManager가 전체 게임 상태의 직렬화/역직렬화를 통합
3. 새 저장 섹션 추가 시 GameStorageManager만 확장


---


## Class Design


```
GameStorageManager : CompoSingleton<GameStorageManager> (MobileSystem 레이어)
│
├── Constants
│   └── CurrentVersion = 2
│
├── Fields
│   └── _inventory : InventoryStorage (InventoryManager.Instance에서 획득)
│   └── _purchase : PurchaseStorage (GameStorageManager 소유)
│
├── Public Properties
│   └── Purchase : PurchaseStorage
│
├── Public Methods
│   ├── ToJson() → string
│   ├── LoadFromPayload(string payload) → void
│   ├── LoadFromJson(string json) → void
│   └── Clear()
│
├── Private Methods
│   ├── _serializeInventory() → JObject
│   └── _deserializeInventory(JObject inv) → void
│   ├── _serializePurchase() → JObject
│   └── _deserializePurchase(JObject purchase) → void
│
└── 확장 예정
    ├── (향후) missions 섹션
    └── (향후) player 섹션
```

### Singleton

```csharp
GameStorageManager : CompoSingleton<GameStorageManager>
```

- `CompoSingleton<GameStorageManager>` 패턴으로 싱글톤 등록.
- 접근: `GameStorageManager.Instance`


### _inventory 필드

- `_inventory = InventoryManager.Instance.Storage`로 InventoryStorage를 참조한다.
- GameStorageManager는 InventoryStorage를 소유하지 않는다 (InventoryManager가 소유).
- GameStorageManager는 InventoryManager 싱글톤에 의존한다.

### _purchase 필드

- `_purchase : PurchaseStorage`는 **GameStorageManager가 직접 소유**한다.
- PurchaseManager는 `GameStorageManager.Instance.Purchase`를 통해 상태를 기록한다.
- 목적은 local/cloud 저장 가능한 **최소 구매 상태 스냅샷**이며, 전체 구매 이력이 아니다.


### ToJson()

`_inventory`의 **ReadOnly 프로퍼티**와 `_purchase` 스냅샷을 사용하여 직렬화:

- `_inventory.Wallet` → `IReadOnlyDictionary<CURRENCY_TYPE, long>`
- `_inventory.Equipments` → `IReadOnlyDictionary<string, AbilityEquip>`
- `_inventory.Cards` → `IReadOnlyDictionary<string, AbilityCard>`
- `_inventory.Heroes` → `IReadOnlyDictionary<string, AbilityUnitHero>`
- `_purchase` → `purchase.current`, `purchase.last` (최소 스냅샷)

직렬화 순서: wallet → equipments → cards → heroes (기존 유지).


### LoadFromPayload()

obfuscated payload를 `ComplexUtil.Decrypt_Base64()`로 복호화한 뒤 `LoadFromJson()`에 위임한다.

```
LoadFromPayload(payload) = LoadFromJson(ComplexUtil.Decrypt_Base64(payload))
```


### LoadFromJson()

`_inventory`의 **public 메서드**와 `_purchase` restore API를 사용하여 역직렬화:

- `_inventory.Clear()`
- `_inventory.AddCurrency()`, `_inventory.AddEquip()`, `_inventory.AddCard()`, `_inventory.AddHero()`
- `_purchase.ClearAll()`, `_purchase.RestoreCurrent()`, `_purchase.RestoreLast()`

역직렬화 순서: wallet → equipments → cards → heroes (equip slot 참조를 위해 heroes는 마지막).


---


## JSON Schema


```json
{
  "version": 2,
  "inventory": {
    "wallet": {
      "<CURRENCY_TYPE.ToString()>": "<long>"
    },
    "equipments": {
      "<itemUid>": {
        "equipId": "<string>",
        "itemUid": "<string>",
        "ownerUnitId": "<string>",
        "ownerSlotNumber": "<int>",
        "stats": {
          "<STAT_TYPE.ToString()>": "<int>"
        }
      }
    },
    "cards": {
      "<cardId>": {
        "cardId": "<string>",
        "stats": {
          "<STAT_TYPE.ToString()>": "<int>"
        }
      }
    },
    "heroes": {
      "<heroId>": {
        "unitId": "<string>",
        "stats": {
          "<STAT_TYPE.ToString()>": "<int>"
        },
        "equips": {
          "<slotNumber>": "<equipUid>"
        }
      }
    }
  },
  "purchase": {
    "current": {
      "isPurchaseInProgress": "<bool>",
      "internalProductId": "<string>",
      "kind": "<string>",
      "storeKey": "<string>",
      "startedAtUtcMs": "<long>",
      "isStorePending": "<bool>",
      "storePendingAtUtcMs": "<long>"
    },
    "last": {
      "internalProductId": "<string>",
      "kind": "<string>",
      "storeKey": "<string>",
      "resultStatus": "<string>",
      "errorCode": "<string>",
      "errorMessage": "<string>",
      "updatedAtUtcMs": "<long>"
    }
  }
}
```

### version

- 현재: `2`
- 스키마 변경 시 version을 증가시키고 마이그레이션 코드를 추가한다.
- LoadFromJson()에서 version을 확인하고, 지원하지 않는 버전이면 실패를 반환한다.
- v1 payload 로드 시 `purchase` 섹션은 없는 것으로 간주하고 `_purchase.ClearAll()`로 초기화한다.


### inventory

- 기존 InventoryStorage JSON 스키마와 **100% 동일**.
- inventory 섹션 내부 스키마 변경은 [93-game-inventory-system/03-ssot](../93-game-inventory-system/03-ssot/SKILL.md)를 따른다.

### purchase

- `purchase` 섹션은 `PurchaseStorage`의 **최소 스냅샷(current + last)** 만 저장한다.
- 전체 구매 이력/영수증/토큰/서버 ledger 정보는 저장하지 않는다.
- 정본: [30-purchase-system/33-purchase-storage](../30-purchase-system/33-purchase-storage/SKILL.md)


---


## Hard Rules


### 1) CompoSingleton

- `GameStorageManager : CompoSingleton<GameStorageManager>`.
- 접근: `GameStorageManager.Instance`.


### 2) MobileSystem 레이어

- GameStorageManager는 **MobileSystem** 레이어에 위치한다.
- 경로: `com.devian.samples/Samples~/MobileSystem/Runtime/Storage/`


### 3) InventoryStorage 직렬화 삭제

- `InventoryStorage.ToJson()` / `InventoryStorage.FromJson()` 삭제.
- 직렬화 책임은 **GameStorageManager만** 담당한다.
- InventoryStorage는 **ReadOnly 프로퍼티 + CRUD 메서드**만 제공한다.


### 4) version 필드 필수

- JSON 루트에 `"version"` 필드가 반드시 포함된다.
- 스키마 변경 시 version을 증가시키고, 하위 호환 마이그레이션을 제공한다.
- 현재 구현은 v1(legacy inventory-only)와 v2(inventory + purchase snapshot)를 읽을 수 있어야 한다.


### 5) 직렬화 순서

- inventory 섹션의 직렬화/역직렬화 순서: **wallet → equipments → cards → heroes**.
- heroes가 마지막인 이유: hero의 equip slot 복원 시 mEquipments 참조가 필요.


### 6) ReadOnly 접근

- ToJson()은 InventoryStorage의 ReadOnly 프로퍼티(`Wallet`, `Equipments`, `Cards`, `Heroes`)만 사용한다.
- LoadFromJson()은 InventoryStorage의 public 메서드(`Clear`, `AddCurrency`, `AddEquip`, `AddCard`, `AddHero` 등)를 사용한다.


### 7) InventoryManager 싱글톤 의존

- GameStorageManager는 `_inventory = InventoryManager.Instance.Storage`로 InventoryStorage를 참조한다.


### 8) PurchaseStorage 소유

- GameStorageManager는 `_purchase : PurchaseStorage`를 직접 소유한다.
- `PurchaseStorage`는 local/cloud 저장 가능한 구매 상태 스냅샷이며, 전체 구매 내역 저장소가 아니다.
- PurchaseManager는 `GameStorageManager.Instance.Purchase`에 기록만 수행한다.


---


## Implementation Location (3-path mirror)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Storage/`
- UnityExample/Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Storage/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/Storage/`


---


## InventoryStorage 변경 사항

삭제 대상 (InventoryStorage.cs):

- `ToJson()` 메서드 전체
- `FromJson(string json)` 메서드 전체

유지:

- `Clear()` — GameStorageManager.LoadFromJson()에서 호출
- 모든 ReadOnly 프로퍼티 (Wallet, Equipments, Cards, Heroes)
- 모든 CRUD 메서드 (AddCurrency, AddEquip, AddCard, AddHero 등)


---


## Related

- [00-overview](../00-overview/SKILL.md) — Mobile System 개요
- [12-game-ability](../../40-game-system/12-game-ability/SKILL.md) — Ability 시스템 (직렬화 대상)
- [93-game-inventory-system/03-ssot](../93-game-inventory-system/03-ssot/SKILL.md) — Inventory JSON 스키마 정본
- [93-game-inventory-system/11-inventory-storage](../93-game-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage 설계
- [30-purchase-system/33-purchase-storage](../30-purchase-system/33-purchase-storage/SKILL.md) — PurchaseStorage(구매 상태 스냅샷)
