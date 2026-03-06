---
name: mission-message-system
description: Use this skill when defining or implementing MissionMessageSystem notify contracts between MissionManager and external UI/GameObjects.
---

# 16-mission-message-system

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Message SSOT

## Purpose

Mission 변화 알림 전용 메시지 시스템 정본이다.

---

## Type

```csharp
public sealed class MissionMessageSystem : MessageSystem<EntityId, MISSION_MESSAGE>
{
}
```

규칙:

- MissionManager가 단일 인스턴스를 소유한다.
- MissionRuntime/Scheduler는 직접 notify하지 않는다.
- 외부 구독자는 `MissionManager.Subcribe(...)`, `SubcribeOnce(...)`, `UnSubcribe(...)` 헬퍼를 사용한다.

---

## Message Values

- `RUNTIME_INIT`
- `RUNTIME_PROGRESS`
- `RUNTIME_CLAIMABLE`
- `RUNTIME_REWARDED`
- `DAY_RESET`
- `ACHIEVE_LEVEL_UP`

---

## Payload Rules

- 기본: `args[0] = MissionRuntimeBase`
- 예외: `DAY_RESET`는 no args
- `RUNTIME_REWARDED`: `args[1] = RewardData[]`

---

## Notify Timing

- runtime create/restore 직후: `RUNTIME_INIT`
- progress 변경 직후: `RUNTIME_PROGRESS`
- claimable 전이 시: `RUNTIME_CLAIMABLE`
- claim 성공 보상 적용 후: `RUNTIME_REWARDED`
- daily reset 시: `DAY_RESET`
- achieve level-up 직후: `ACHIEVE_LEVEL_UP`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
