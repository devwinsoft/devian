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
2. GameStorageManager가 전체 게임 상태의 직렬화/역직렬화 진입점을 통합
3. 직렬화 구현은 `GameStorageJsonCodec` + 섹션 codec(`Inventory`, `Purchase`)로 분리하여 파일 책임을 축소
4. 새 저장 섹션 추가 시 GameStorageManager(진입점) + GameStorageJsonCodec(root orchestration) + 섹션 codec(구현)를 함께 확장


---


## Class Design


```
GameStorageManager : CompoSingleton<GameStorageManager> (MobileSystem 레이어)
│
├── Fields
│   └── _inventory : InventoryStorage (InventoryManager.Instance에서 획득)
│   └── _purchase : PurchaseStorage (GameStorageManager 소유)
│
├── Public Properties
│   ├── Inventory : InventoryStorage (read-only, InventoryManager.Instance.Storage 참조)
│   └── Purchase : PurchaseStorage
│
├── Public Methods
│   ├── ToJson() → string
│   ├── LoadFromPayload(string payload) → void
│   ├── LoadFromJson(string json) → void
│   └── Clear()
│
├── Delegates To
│   ├── GameStorageJsonCodec (root version / inventory,purchase 섹션 orchestration)
│   ├── GameStorageJsonCodecInventory (inventory 섹션 serialize/deserialize)
│   └── GameStorageJsonCodecPurchase (purchase 섹션 serialize/deserialize)
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
- `_purchase` → `purchase.current` (진행 중 결제 복구 상태)

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
- `_purchase.ClearAll()`, `_purchase.RestoreCurrent()`, `_purchase.RestoreRefundSupportLogs()`

역직렬화 순서: wallet → equipments → cards → heroes (equip slot 참조를 위해 heroes는 마지막).


---


## JSON Schema


```json
{
  "version": 6,
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
    "noAdsExpireAtClientUtcMs": "<long>",
    "seasonPassOwnership": {
      "<internalProductId>": "<bool>"
    },
    "current": {
      "isPurchaseInProgress": "<bool>",
      "internalProductId": "<string>",
      "kind": "<string>",
      "storeKey": "<string>",
      "startedAtUtcMs": "<long>",
      "isStorePending": "<bool>",
      "storePendingAtUtcMs": "<long>",
      "purchaseId": "<string>",
      "verifyStatus": "<string>",
      "clientGrantApplied": "<bool>",
      "clientGrantReported": "<bool>"
    },
    "refundSupportLogs": [
      {
        "purchaseId": "<string>",
        "internalProductId": "<string>",
        "kind": "<string>",
        "storeKey": "<string>",
        "verifyStatus": "<string>",
        "clientGrantStatus": "<string>",
        "storeConfirmStatus": "<string>",
        "firstSeenAtUtcMs": "<long>",
        "lastUpdatedAtUtcMs": "<long>"
      }
    ]
  }
}
```

### version

- 현재: `7`
- 스키마 변경 시 version을 증가시키고 마이그레이션 코드를 추가한다.
- LoadFromJson()에서 version을 확인하고, 지원하지 않는 버전이면 실패를 반환한다.
- v1 payload 로드 시 `purchase` 섹션은 없는 것으로 간주하고 `_purchase.ClearAll()`로 초기화한다.
- v2 payload 로드 시 `purchase.last`는 무시하고 `purchase.current`만 복원한다.
- v5 payload부터 `purchase.refundSupportLogs[]`를 함께 복원한다. (없으면 빈 배열)
- v6 payload의 legacy `purchase.noAds`(bool) 필드는 무시하고, `purchase.seasonPassOwnership`만 복원한다. (없으면 기본값)
- v7 payload부터 `purchase.noAdsExpireAtClientUtcMs`(long, client clock)와 `purchase.seasonPassOwnership`를 복원한다. (없으면 기본값)
- v8 payload부터 `purchase.current.storeConfirmedLocal`을 복원한다. (없으면 `false`)


### inventory

- 기존 InventoryStorage JSON 스키마와 **100% 동일**.
- inventory 섹션 내부 스키마 변경은 [93-game-inventory-system/03-ssot](../93-game-inventory-system/03-ssot/SKILL.md)를 따른다.

### purchase

- `purchase` 섹션은 `PurchaseStorage`의 **current(진행 중 결제 복구 상태)**, **refundSupportLogs(환불/지원 대응용 최소 로그)**, **game logic cache(noAdsExpireAtClientUtcMs)**, **entitlement cache(seasonPassOwnership)** 를 저장한다.
- `ToJson()` 경로에서 `PurchaseStorage.PruneRefundSupportLogs()`를 먼저 호출하여 TTL/Cap 정책을 적용한 뒤 직렬화한다.
- 전체 구매 이력/영수증/토큰/서버 ledger 정보는 저장하지 않는다.
- 구매 실패 내역(에러 코드/메시지)도 저장하지 않는다.
- 정본: [30-purchase-system/33-purchase-storage](../30-purchase-system/33-purchase-storage/SKILL.md)


---


## SaveDataManager와의 관계

GameStorageManager와 SaveDataManager는 **별도의 관심사**를 담당한다.

| | **SaveDataManager** | **GameStorageManager** |
|---|---|---|
| 책임 | 영속화 엔진 (Local/Cloud I/O, Sync, Conflict) | 직렬화 컨테이너 (ToJson, LoadFromJson) |
| 소유 데이터 | 슬롯 설정, deviceId, saveSeq | PurchaseStorage, InventoryStorage 참조 |
| 서버 연동 | Firestore (Cloud Save) | 없음 |

### 데이터 흐름 (호출 측 배선)

SaveDataManager는 난독화된 payload blob을 반환하고, GameStorageManager는 이를 인메모리 게임 상태로 역직렬화한다.
**두 매니저 간의 연결은 호출 측이 수동으로 수행한다.**

```
[Load 경로]
SaveDataManager.SyncAsync(slot, ct) → SyncResult (난독화 payload 포함)
    ↓ 호출 측
GameStorageManager.LoadFromPayload(payload) → _inventory, _purchase 복원

[Save 경로]
GameStorageManager.ToJson() → JSON string
    ↓ 호출 측
SaveDataManager.SaveDataAsync(slot, data, includeCloud, ct) → Local/Cloud 저장
```

- 표준 Post-Sync 오케스트레이션 순서는 [30-purchase-system/30-samples-purchase-manager](../30-purchase-system/30-samples-purchase-manager/SKILL.md) §Post-Sync Orchestration을 참조한다.


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
- 직렬화 **진입점/상태 소유 책임**은 GameStorageManager가 담당한다.
- JSON 직렬화/역직렬화 구현은 `GameStorageJsonCodec`(root) + `GameStorageJsonCodecInventory` / `GameStorageJsonCodecPurchase`(섹션 구현)로 분리한다.
- InventoryStorage는 **ReadOnly 프로퍼티 + CRUD 메서드**만 제공한다.


### 4) version 필드 필수

- JSON 루트에 `"version"` 필드가 반드시 포함된다.
- 스키마 변경 시 version을 증가시키고, 하위 호환 마이그레이션을 제공한다.
- 현재 구현은 v1(legacy inventory-only), v2(purchase current+last), v3(purchase current-only, `serverAcked`), v4(purchase current-only, `clientGrantReported`), v5(purchase current + `refundSupportLogs`), v6(purchase legacy `noAds` + `seasonPassOwnership`), v7(purchase `noAdsExpireAtClientUtcMs` + `seasonPassOwnership`), v8(purchase `current.storeConfirmedLocal`)를 읽을 수 있어야 한다.
- `refundSupportLogs` 보관 정책(TTL 30일 + 32개 cap)은 schema 변경이 아니므로 버전 증가 사유가 아니다.


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

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/03-ssot](../../../03-ssot/SKILL.md) §UPM Packages Sync

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Storage/`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Storage/`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Storage/`
- 핵심 파일:
  - `GameStorageManager.cs` (상태 소유 + ToJson/LoadFromJson 진입점)
  - `GameStorageJsonCodec.cs` (root JSON serialize/deserialize orchestration, version/migration 포함)
  - `GameStorageJsonCodecInventory.cs` (inventory 섹션 serialize/deserialize)
  - `GameStorageJsonCodecPurchase.cs` (purchase 섹션 serialize/deserialize)


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
