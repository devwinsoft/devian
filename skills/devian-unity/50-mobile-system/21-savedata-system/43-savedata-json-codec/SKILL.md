# 43-savedata-json-codec — SaveDataJsonCodec


Status: ACTIVE
AppliesTo: v10
Type: Design / SSOT


## Purpose

SaveDataManager가 local/cloud payload에 저장할 게임 상태 JSON의 직렬화/역직렬화 규약을 정의한다.

- JSON 직렬화/역직렬화의 **유일한 진입점은 SaveDataManager** 이다.
- `AccountManager`, `InventoryManager`, `PurchaseManager`, `MissionManager`는 각자 자신의 `Storage`를 소유한다.
- JSON 구현은 `SaveDataJsonCodec` root codec + section codec으로 분리한다.


---


## Ownership

- `AccountManager` 소유: `AccountStorage`
- `InventoryManager` 소유: `InventoryStorage`
- `PurchaseManager` 소유: `PurchaseStorage`
- `MissionManager` 소유: `MissionStorage`
- `SaveDataManager` 책임:
  - 위 4개 storage를 수집
  - primary save binding(local filename + cloud slot) 관리
  - JSON serialize/deserialize 호출
  - payload encrypt/decrypt
  - local/cloud save, load, sync, conflict

중요:
- `GameStorageManager`는 삭제되었다.
- 각 manager는 자신의 storage를 직접 소유하며, SaveDataManager가 이를 묶어 저장한다.


---


## Class Boundary

```text
SaveDataManager : CompoSingleton<SaveDataManager>
│
├── ToJson()
│   └── SaveDataJsonCodec.Serialize(inventory, purchase, account, mission)
│
├── LoadFromJson(json)
│   └── SaveDataJsonCodec.DeserializeInto(json, inventory, purchase, account, mission)
│
├── LoadFromPayload(payload)
│   └── ComplexUtil.Decrypt_Base64(payload) -> LoadFromJson(json)
│
└── ClearGameState()
    ├── AccountManager.Instance.Storage.Clear()
    ├── InventoryManager.Instance.Storage.Clear()
    ├── PurchaseManager.Instance.Storage.ClearAll()
    └── MissionManager.Instance.Storage.Clear()
```

- root orchestration: `SaveDataJsonCodec`
- inventory section: `SaveDataJsonCodecInventory`
- purchase section: `SaveDataJsonCodecPurchase`
- account section: `SaveDataJsonCodecAccount`
- mission section: `SaveDataJsonCodecMission`

section codec은 manager를 직접 알지 않고, `Storage` 타입만 다룬다.


## Primary Save Rule

- public save API는 멀티 슬롯을 노출하지 않는다.
- `SaveDataManager`는 단일 primary save binding만 가진다.
- codec은 binding을 모르고, 순수하게 게임 상태 JSON만 다룬다.


---


## Root JSON Schema

```json
{
  "version": 10,
  "inventory": {},
  "purchase": {},
  "account": {},
  "mission": {}
}
```

- `version`: root schema version
- `inventory`: `InventoryStorage` 섹션
- `purchase`: `PurchaseStorage` 섹션
- `account`: `AccountStorage` 섹션
- `mission`: `MissionStorage` 섹션

payload wrapper(`SaveLocalPayload`, `SaveCloudPayload`)의 `account` 메타는 유지한다.
이 메타는 sync 초기 판정과 account mirror 용도이며, root JSON의 `account` 섹션과 별개로 존재할 수 있다.


---


## Section Sourcing

serialize 시 source:
- `AccountManager.Instance.Storage`
- `InventoryManager.Instance.Storage`
- `PurchaseManager.Instance.Storage`
- `MissionManager.Instance.Storage`

deserialize 시 target:
- `AccountManager.Instance.Storage`
- `InventoryManager.Instance.Storage`
- `PurchaseManager.Instance.Storage`
- `MissionManager.Instance.Storage`

로드 직후 런타임 재적용:
- `AccountManager.ApplyStorage(AccountManager.Instance.Storage)`
- 필요 시 `PurchaseManager.SyncEntitlementsAsync(ct)`로 rental stale 상태 보정
- Mission은 `MissionManager`가 자신의 `MissionStorage`를 기준으로 runtime state를 재구성한다
- Mission claim 직후에는 `SaveDataManager.SaveGameStorageAsync(true, ct)`로 mission 섹션을 포함한 root payload 저장을 즉시 시도해야 한다
- 이 local save가 실패하면 mission 시스템은 플레이 불가능 상태로 처리한다(TODO)


---


## Version / Migration Rules

- schema 변경 시 root `version`을 증가시킨다.
- 하위호환이 필요하면 root codec에서 version 분기 처리한다.
- section codec은 자신이 담당하는 섹션의 레거시 필드만 복구한다.
- 지원하지 않는 version이면 조용히 부분 로드하지 말고, 명시적인 fallback 정책을 문서에 기록한다.


---


## Hard Rules

- manager가 자신의 storage를 소유하더라도, **payload JSON 직렬화 진입점은 SaveDataManager 하나만 유지**한다.
- `InventoryStorage`, `PurchaseStorage`, `AccountStorage`, `MissionStorage`에 `ToJson()` / `FromJson()`을 다시 추가하지 않는다.
- codec은 domain rule을 가지지 않는다. domain mutation은 각 manager가 담당한다.
- 새 저장 섹션 추가 시:
  - root codec 수정
  - section codec 추가
  - SaveDataManager sourcing 연결
  - 관련 manager skill 문서 갱신


---


## Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodec.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecInventory.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecPurchase.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecAccount.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecMission.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/...`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/SaveData/JsonCodec/...`


---


## Related

- [10-savedata-manager](../10-savedata-manager/SKILL.md) — SaveDataManager 진입점
- [41-savedata-savelocal](../41-savedata-savelocal/SKILL.md) — Local payload 저장
- [42-savedata-savecloud](../42-savedata-savecloud/SKILL.md) — Cloud payload 저장
- [33-account-manager](../../20-account-system/33-account-manager/SKILL.md) — AccountStorage 소유자
- [10-inventory-manager](../../93-game-inventory-system/10-inventory-manager/SKILL.md) — InventoryStorage 소유자
- [33-purchase-storage](../../30-purchase-system/33-purchase-storage/SKILL.md) — PurchaseStorage 규약
- [12-mission-storage](../48-mission-system/12-mission-storage/SKILL.md) — MissionStorage 규약
