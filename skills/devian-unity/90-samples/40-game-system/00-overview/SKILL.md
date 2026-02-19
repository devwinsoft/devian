# devian-unity/40-game-system — Overview

Status: ACTIVE
AppliesTo: v10

Game System은 **Devian Samples**(`com.devian.samples`)에 포함된 Unity Sample이다.
이 그룹은 Game 도메인/컨텐츠 샘플(및 그 하위 샘플들)의 스킬을 담당한다.

---

## Sample SSOT
- `com.devian.samples/Samples~/GameContents`

---

## Sub-skills

- [11-domain-game](../11-domain-game/SKILL.md) — Game 도메인 허브 (테이블, 컨트랙트, 생성물, 프로토콜)
- [12-game-ability](../12-game-ability/SKILL.md) — Ability 시스템 (BaseAbility, ItemAbility)
- [13-game-stat-type](../13-game-stat-type/SKILL.md) — StatType enum 정의 (ItemCount, ItemLevel, ItemSlotNumber 등)
- [21-game-net-manager](../21-game-net-manager/SKILL.md) — Unity Network 샘플 (GameNetManager / Game2CStub)

> **Note:** PurchaseManager, RewardManager, InventoryManager(+InventoryStorage)는 [50-mobile-system](../../50-mobile-system/00-overview/SKILL.md)로 이전되었다.

---

## Related

- [50-mobile-system](../../50-mobile-system/00-overview/SKILL.md)
- [08-samples-authoring-guide](../../../08-samples-authoring-guide/SKILL.md)
- [09-samples-creation](../../../09-samples-creation/SKILL.md)
