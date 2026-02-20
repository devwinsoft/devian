# 40-game-system / 16-game-storage-manager — GameStorageManager


Status: ACTIVE
AppliesTo: v10
Type: Design / SSOT


## Purpose

게임 저장 파일(JSON 형식)의 **통합 직렬화/역직렬화**를 담당하는 상위 저장 컨테이너.
InventoryStorage를 포함하여, 향후 다른 게임 데이터(미션 진행도, 플레이어 프로필 등)도 포괄한다.


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
GameStorageManager : CompoSingleton<GameStorageManager> (GameContents 레이어)
│
├── Constants
│   └── CurrentVersion = 1
│
├── Fields
│   └── _inventory : InventoryStorage (InventoryManager.Instance에서 획득)
│
├── Public Methods
│   ├── ToJson() → string
│   ├── ToPayload() → string (obfuscated)
│   ├── LoadFromPayload(string payload) → void
│   ├── LoadFromJson(string json) → void
│   └── Clear()
│
├── Private Methods
│   ├── _serializeInventory() → JObject
│   └── _deserializeInventory(JObject inv) → void
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


### ToJson()

`_inventory`의 **ReadOnly 프로퍼티**를 사용하여 직렬화:

- `_inventory.Wallet` → `IReadOnlyDictionary<CURRENCY_TYPE, long>`
- `_inventory.Equipments` → `IReadOnlyDictionary<string, AbilityEquip>`
- `_inventory.Cards` → `IReadOnlyDictionary<string, AbilityCard>`
- `_inventory.Heroes` → `IReadOnlyDictionary<string, AbilityUnitHero>`

직렬화 순서: wallet → equipments → cards → heroes (기존 유지).


### ToPayload()

`ToJson()` 결과를 `ComplexUtil.Encrypt_Base64()`로 난독화하여 반환한다.
bootstrap(composition root)에서 저장 시스템에 전달하는 obfuscated string을 생성한다.

```
ToPayload() = ComplexUtil.Encrypt_Base64(ToJson())
```


### LoadFromPayload()

obfuscated payload를 `ComplexUtil.Decrypt_Base64()`로 복호화한 뒤 `LoadFromJson()`에 위임한다.

```
LoadFromPayload(payload) = LoadFromJson(ComplexUtil.Decrypt_Base64(payload))
```


### LoadFromJson()

`_inventory`의 **public 메서드**를 사용하여 역직렬화:

- `_inventory.Clear()`
- `_inventory.AddCurrency()`, `_inventory.AddEquip()`, `_inventory.AddCard()`, `_inventory.AddHero()`

역직렬화 순서: wallet → equipments → cards → heroes (equip slot 참조를 위해 heroes는 마지막).


---


## JSON Schema


```json
{
  "version": 1,
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
  }
}
```

### version

- 현재: `1`
- 스키마 변경 시 version을 증가시키고 마이그레이션 코드를 추가한다.
- LoadFromJson()에서 version을 확인하고, 지원하지 않는 버전이면 실패를 반환한다.


### inventory

- 기존 InventoryStorage JSON 스키마와 **100% 동일**.
- inventory 섹션 내부 스키마 변경은 [15-game-inventory-system/03-ssot](../15-game-inventory-system/03-ssot/SKILL.md)를 따른다.


---


## Hard Rules


### 1) CompoSingleton

- `GameStorageManager : CompoSingleton<GameStorageManager>`.
- 접근: `GameStorageManager.Instance`.


### 2) GameContents 레이어

- GameStorageManager는 **GameContents** 레이어에 위치한다.
- 경로: `com.devian.samples/Samples~/GameContents/Runtime/Storage/`


### 3) InventoryStorage 직렬화 삭제

- `InventoryStorage.ToJson()` / `InventoryStorage.FromJson()` 삭제.
- 직렬화 책임은 **GameStorageManager만** 담당한다.
- InventoryStorage는 **ReadOnly 프로퍼티 + CRUD 메서드**만 제공한다.


### 4) version 필드 필수

- JSON 루트에 `"version"` 필드가 반드시 포함된다.
- 스키마 변경 시 version을 증가시키고, 하위 호환 마이그레이션을 제공한다.


### 5) 직렬화 순서

- inventory 섹션의 직렬화/역직렬화 순서: **wallet → equipments → cards → heroes**.
- heroes가 마지막인 이유: hero의 equip slot 복원 시 mEquipments 참조가 필요.


### 6) ReadOnly 접근

- ToJson()은 InventoryStorage의 ReadOnly 프로퍼티(`Wallet`, `Equipments`, `Cards`, `Heroes`)만 사용한다.
- LoadFromJson()은 InventoryStorage의 public 메서드(`Clear`, `AddCurrency`, `AddEquip`, `AddCard`, `AddHero` 등)를 사용한다.


### 7) InventoryManager 싱글톤 의존

- GameStorageManager는 `_inventory = InventoryManager.Instance.Storage`로 InventoryStorage를 참조한다.


---


## Implementation Location (3-path mirror)

- UPM: `framework-cs/upm/com.devian.samples/Samples~/GameContents/Runtime/Storage/`
- UnityExample/Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/GameContents/Runtime/Storage/`
- Assets/Samples: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/GameContents/Runtime/Storage/`


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

- [00-overview](../00-overview/SKILL.md) — Game System 개요
- [12-game-ability](../12-game-ability/SKILL.md) — Ability 시스템 (직렬화 대상)
- [15-game-inventory-system/03-ssot](../15-game-inventory-system/03-ssot/SKILL.md) — Inventory JSON 스키마 정본
- [15-game-inventory-system/11-inventory-storage](../15-game-inventory-system/11-inventory-storage/SKILL.md) — InventoryStorage 설계
