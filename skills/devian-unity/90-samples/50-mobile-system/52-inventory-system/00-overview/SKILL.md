# 52-inventory-system — Overview


Status: ACTIVE
AppliesTo: v10


Inventory System은 "인벤토리 상태(아이템/통화)"를 시스템 레이어에서 관리하기 위한 스킬 그룹이다.
컨텐츠 정의를 직접 참조하지 않고, `RewardData` 규약(정본: 49-reward-system)을 입력 계약으로 사용한다.


---


## Scope

- `RewardData[]`를 인벤토리 상태에 적용(Apply)한다.
- 인벤토리 상태 조회 API(개념)를 제공한다.
- 변경 이벤트(개념)를 제공한다.


## Non-goals

- 컨텐츠 도메인 테이블/enum을 직접 참조하지 않는다.
- 보상 정의 테이블 → Delta 변환은 컨텐츠 레이어 책임이다.
- 멱등/기록/복구(ledger)는 호출자(Mission/Purchase)가 책임진다.


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰(컨텐츠 미의존, Delta 규약 고정) |
| [03-ssot](../03-ssot/SKILL.md) | RewardData 참조 규약 + Inventory 적용 규칙 |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-inventory-manager](../10-inventory-manager/SKILL.md) | InventoryManager 설계(필수) |
| [11-inventory-storage](../11-inventory-storage/SKILL.md) | InventoryStorage / ItemData (데이터 컨테이너) |


---


## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
- [30-purchase-system](../../30-purchase-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
