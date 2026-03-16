# 45-game-message-system — Overview

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 메시지/트리거/메시지 저장 계층 개요다.

핵심:
- `GameMessageManager`가 game message trigger와 `GameMessageStorage`를 소유한다.
- Mission 입력(`Notify`)의 stat 누적 정본은 `message.stats[messageId]`다.
- SaveData payload에서 message stat 저장 위치는 root `message` 섹션이다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | 타입/Notify/구독 정본 |
| [10-game-message-manager](../10-game-message-manager/SKILL.md) | GameMessageManager 설계 |
| [11-game-message-trigger](../11-game-message-trigger/SKILL.md) | GameMessageTrigger 설계 |
| [14-game-message-storage](../14-game-message-storage/SKILL.md) | GameMessageStorage 저장 모델 |

---

## Related

- [48-mission-system](../../48-mission-system/00-overview/SKILL.md)
- [48-mission-system/16-mission-message-trigger](../../48-mission-system/16-mission-message-trigger/SKILL.md)
- [46-achieve-system](../../46-achieve-system/00-overview/SKILL.md)
- [20-common-package/25-trigger](../../../20-common-package/25-trigger/SKILL.md)
