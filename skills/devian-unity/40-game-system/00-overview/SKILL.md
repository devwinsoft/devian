# devian-unity/40-game-system — Overview

Status: ACTIVE
AppliesTo: v10

Game System은 **Devian Samples**(`com.devian.samples`)에 포함된 Unity Sample이다.
이 그룹은 Game 도메인/컨텐츠 샘플(및 그 하위 샘플들)의 스킬을 담당한다.

---

## Sample SSOT
- `com.devian.samples/Samples~/GameContents`

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Game System 그룹 정책 (Templates 규칙) |

---

## Sub-skills

- [11-game-domain](../11-game-domain/SKILL.md) — Game 도메인 허브 (테이블, 컨트랙트, 생성물, 프로토콜)
- [12-game-ability](../12-game-ability/SKILL.md) — Ability 시스템 (AbilityBase, AbilityEquip, AbilityCard)
- [13-game-stat-type](../13-game-stat-type/SKILL.md) — STAT_TYPE enum 정의 (CARD_AMOUNT, UNIT_AMOUNT, EQUIP_LEVEL 등)
- [14-game-protocol](../14-game-protocol/SKILL.md) — Game 프로토콜 예제 (C2Game, Game2C)
- [93-game-inventory-system](../../50-mobile-system/93-game-inventory-system/00-overview/SKILL.md) — Inventory System (InventoryManager, InventoryStorage, moved to 50-mobile-system)
- [91-game-net-manager](../../50-mobile-system/91-game-net-manager/SKILL.md) — Unity Network 샘플 (GameNetManager / Game2CStub, moved to 50-mobile-system)

---

## Related

- [50-mobile-system](../../50-mobile-system/00-overview/SKILL.md)
- [07-samples-creation-guide](../../07-samples-creation-guide/SKILL.md)
