# 11-mission-trigger-system

Status: ACTIVE
AppliesTo: v10


## Overview

Mission 전용 trigger 시스템. `MessageSystem<int, MISSION_CONDITION_TYPE>`를 특화한 인스턴스 클래스다.
MissionManager가 단일 인스턴스를 소유하고, 각 MissionRuntime이 여기에 직접 구독한다.


---


## Terms

| Term | Definition |
|------|------------|
| `MissionTriggerSystem` | `MessageSystem<int, MISSION_CONDITION_TYPE>` 특화 클래스 |
| `MISSION_CONDITION_TYPE` | 미션 조건 타입 enum (`ENUM_MISSION.json` source) |
| `ownerKey` | `int`; `missionUid`를 그대로 사용 |
| `msgValue` | 진행도 갱신에 사용하는 `CBigInt` 또는 `long` payload |


---


## SSOT

### Type

```csharp
namespace Devian
{
    public class MissionTriggerSystem : MessageSystem<int, MISSION_CONDITION_TYPE>
    {
    }
}
```

### Owner

MissionManager가 `MissionTriggerSystem`의 유일한 인스턴스를 소유한다.

```csharp
private MissionTriggerSystem mTriggerSystem = new MissionTriggerSystem();
public static MissionTriggerSystem triggerSystem => Instance.mTriggerSystem;
```

규칙:
- MissionManager는 `CompoSingleton<MissionManager>`다.
- `mTriggerSystem`은 field initializer에서 즉시 생성한다.
- `triggerSystem` 접근에 optional/null 허용을 두지 않는다.

### Subscription

MissionRuntime은 runtime별 ownerKey를 사용해 자신의 `conditionType`을 직접 구독한다.

```csharp
mTriggerSystem.Subcribe(runtimeOwnerKey, MISSION_CONDITION_TYPE.STAGE_CLEAR, onTrigger);
```


---


## Notify Contract

메시지 발행자는 아래 형태를 사용한다.

```csharp
MissionManager.triggerSystem.Notify(msgType, msgValue);
```

| arg index | type | note |
|----------|------|------|
| `args[0]` | `CBigInt` or `long` | `msgValue` |

규칙:
- 인자 수가 부족하거나 타입이 맞지 않으면 MissionRuntime은 해당 notify를 무시한다.
- `MessageSystem` 자체는 replay/dedup/queue 기능이 없다.
- `MessageSystem`은 중복 구독을 허용한다. 중복 초기화를 막는 책임은 사용 측에 있다.
- daily MissionRuntime은 `CLAIMABLE`/`COMPLETED` 시 구독을 해지한다.
- achievement MissionRuntime은 삭제/파기 전까지 구독을 유지한다.


---


## MISSION_CONDITION_TYPE Values

| Value | Purpose |
|-------|---------|
| `NONE` | 기본값 |
| `LOGIN` | 로그인/출석 계열 진행도 입력 |
| `STAGE_CLEAR` | 스테이지 클리어 계열 진행도 입력 |
| `ACHIEVEMENT_UNLOCKED` | 업적 달성 입력 |

정본 source:
- `input/Domains/Game/ENUM_MISSION.json`


---


## MissionRuntimeBase Recording Rules

`MissionTriggerSystem`은 trigger를 전달만 하고, MissionRuntime이 자신의 `ProgressValue`를 직접 갱신한다.

갱신/읽기 규칙:
| `MISSION_OP_TYPE` | Rule |
|-----------------|------|
| `NONE` | 갱신하지 않음 |
| `MAX` | `runtime.progressValue`를 max 갱신 |
| `SUM` | 누적형 progress. clamp 여부는 concrete runtime이 결정 |

claim 가능 판정:
- `runtime.progressValue >= conditionValue && runtime.isCompleted == false`이면 claimable


---


## Reference

- [22-message-system](../../20-domain-common-system/25-message-system/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
