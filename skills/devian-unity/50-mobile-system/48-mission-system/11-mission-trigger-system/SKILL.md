# 11-mission-trigger-system

Status: ACTIVE  
AppliesTo: v10

## Overview

Mission 전용 trigger 라우터다.

- 타입: `MessageSystem<int, MISSION_STAT_TYPE>`
- ownerKey: `missionUid`
- MissionManager가 단일 인스턴스를 소유한다.

---

## Contract

```csharp
public sealed class MissionTriggerSystem : MessageSystem<int, MISSION_STAT_TYPE>
{
}
```

규칙:

- TriggerSystem은 순수 구독 라우터다.
- 외부 입력 진입점은 `MissionManager.Notify(...)`다.
- `stats` 선갱신과 notify 순서 제어는 MissionManager 책임이다.
- TriggerSystem 자체는 큐/재생/영속성 책임이 없다.

---

## Subscription

- runtime은 `missionUid`를 ownerKey로 사용한다.
- daily:
  - active 구간에서만 구독
  - claimable/completed 시 구독 해지
- achieve:
  - runtime 존재 동안 구독 유지
  - level-up 시 기존 구독 해지 후 새 statType으로 재구독

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
