# 51-treasure-system — Overview

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 Treasure 시스템 개요다.
Treasure 시스템은 "등급별 chest 보유량 + chest(exp/level) 상태 관리 + reward 지급 orchestration"을 담당한다.

- `TreasureManager`가 `InventoryStorage`의 treasure 필드를 통해 상태를 관리한다.
- chest 수집은 `TREASURE_REWARD(gradeType)` -> `RewardManager` 경로로 지급한다.
- current chest 수집은 `TREASURE_CHEST(level)` -> `TREASURE_REWARD(gradeType)` -> `RewardManager` 경로로 지급한다.
- chest 상태는 `ITEM_GRADE_TYPE`별 count로 관리한다 (`InventoryStorage.TreasureCounts`).
- chest exp/level 상태는 `InventoryTreasureCurrent` (`TreasureCurrent.Exp`, `TreasureCurrent.Level`)로 관리한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰/API 규약 |
| [03-ssot](../03-ssot/SKILL.md) | `TREASURE_CHEST` / `TREASURE_REWARD` 정본 |
| [10-treasure-manager](../10-treasure-manager/SKILL.md) | TreasureManager 설계와 구현 계획 |

---

## Related

- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
- [11-game-tables](../../../../devian/21-domain-game/11-game-tables/SKILL.md)
- [MobilePackage Overview](../../00-overview/SKILL.md)
