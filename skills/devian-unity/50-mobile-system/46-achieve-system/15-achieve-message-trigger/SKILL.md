---
name: achieve-message-trigger
description: Use this skill when defining or implementing AchieveMessageTrigger notify contracts between AchieveManager and external UI/GameObjects in MobileSystem.
---

# 15-achieve-message-trigger

Status: ACTIVE
AppliesTo: v10
Type: Design / Message SSOT

## Purpose

업적 상태 변화 알림 전용 메시지 트리거 정본이다.

---

## Type

```csharp
public sealed class AchieveMessageTrigger : BaseTrigger<EntityId, ACHIEVE_MESSAGE>
{
}
```

규칙:

- `AchieveManager`가 단일 인스턴스를 소유한다.
- 외부 구독자는 `AchieveManager.Subcribe(...)`, `SubcribeOnce(...)`, `UnSubcribe(...)` 헬퍼를 사용한다.
- 업적 진행 입력(`GAME_MESSAGE_TYPE`)은 `GameMessageManager.Notify(...)` 경계에서 처리되고, `AchieveMessageTrigger`는 알림 전용이다.

---

## Message Values

- `RUNTIME_INIT`
- `RUNTIME_PROGRESS`
- `RUNTIME_CLAIMABLE`
- `RUNTIME_LEVEL_UP`
- `RUNTIME_REWARDED`
- `ACHIEVEMENT_UNLOCKED`

---

## Payload Rules

- `RUNTIME_*`: `args[0] = AchieveRuntime`
- `RUNTIME_REWARDED`: `args[1] = RewardData[]`
- `ACHIEVEMENT_UNLOCKED`: `args[0] = string achievementId`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-achieve-manager](../10-achieve-manager/SKILL.md)
- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
