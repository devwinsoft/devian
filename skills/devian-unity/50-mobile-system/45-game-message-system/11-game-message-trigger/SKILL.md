# 11-game-message-trigger

Status: ACTIVE
AppliesTo: v10

## Overview

Game message 입력 라우터다.

- 타입: `BaseTrigger<int, GAME_MESSAGE_TYPE>`
- ownerKey: subscriber-defined `int`
- 실소유자는 `GameMessageManager`다.

---

## Contract

```csharp
public sealed class GameMessageTrigger : BaseTrigger<int, GAME_MESSAGE_TYPE>
{
}
```

규칙:

- TriggerSystem은 순수 구독 라우터다.
- 외부 입력 진입점은 `MissionManager.Notify(...)`다.
- GameMessageManager는 helper를 통해 publish/subscribe를 중개한다.
- TriggerSystem 자체는 큐/재생/영속성 책임이 없다.

---

## Subscription

- Mission runtime은 MissionManager helper를 통해 game trigger를 직접 구독한다.
- 업적(`ACHIEVE`) notify는 MissionManager 경유로 AchieveManager에 전달된다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-game-message-manager](../10-game-message-manager/SKILL.md)
- [48-mission-system/10-mission-manager](../../48-mission-system/10-mission-manager/SKILL.md)
- [48-mission-system/13-mission-runtime](../../48-mission-system/13-mission-runtime/SKILL.md)
