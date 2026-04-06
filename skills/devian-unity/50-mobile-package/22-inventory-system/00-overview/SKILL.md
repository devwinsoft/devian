# 22-inventory-system — Overview


Status: ACTIVE
AppliesTo: v10


Inventory System은 "인벤토리 상태(아이템/통화)"를 시스템 레이어에서 관리하기 위한 스킬 그룹이다.
컨텐츠 정의를 직접 참조하지 않고, `RewardData` 규약(정본: 49-reward-system)을 입력 계약으로 사용한다.
live runtime state는 `InventoryManager`가 소유하고, save/load는 `InventorySnapshot` DTO를 통해서만 반입/반출한다.


---


## Scope

- `RewardData[]`를 인벤토리 상태에 적용(Apply)한다.
- `InventoryManager` 단일 boundary로 인벤토리 상태 조회 API를 제공한다.
- 일반 delta 메시지와 bulk snapshot 메시지를 제공한다.


## Non-goals

- 컨텐츠 도메인 테이블/enum을 직접 참조하지 않는다.
- 보상 정의 테이블 → Delta 변환은 컨텐츠 레이어 책임이다.
- 멱등/기록/복구(ledger)는 호출자가 책임진다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰(컨텐츠 미의존, Delta 규약 고정) |
| [03-ssot](../03-ssot/SKILL.md) | RewardData 참조 규약 + Inventory 적용 규칙 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-inventory-manager](../10-inventory-manager/SKILL.md) | InventoryManager 설계(필수) |
| [11-inventory-storage](../11-inventory-storage/SKILL.md) | InventoryStorage (데이터 컨테이너) |
| [12-inventory-wallet](../12-inventory-wallet/SKILL.md) | Currency State Flattening (legacy wallet wrapper 제거) |
| [13-inventory-settings](../13-inventory-settings/SKILL.md) | InventorySettings (설정 ScriptableObject, AES+CInt) |
| [14-inventory-stamina-controller](../14-inventory-stamina-controller/SKILL.md) | InventoryStaminaController (설정 로드 + 스태미나 회복) |
| [16-inventory-message-trigger](../16-inventory-message-trigger/SKILL.md) | Inventory 변경 메시지 트리거 |


---


## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
