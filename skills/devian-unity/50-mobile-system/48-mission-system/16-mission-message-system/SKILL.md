---
name: mission-message-system
description: Use this skill when defining or implementing MissionMessageSystem for the mission system, especially when MissionManager must notify external UI GameObjects about mission runtime changes through MessageSystem<EntityId, MISSION_MESSAGE>.
---

# 16-mission-message-system

Status: ACTIVE
AppliesTo: v10
Type: Design / Message SSOT

## Purpose

`MissionMessageSystem`은 mission 변화 알림 전용 로컬 메시지 시스템이다.

- Mission trigger 입력용이 아니다.
- 외부 UI/GameObject 갱신용 notify 채널이다.
- MissionManager가 단일 인스턴스를 소유한다.

---

## Type

```csharp
public sealed class MissionMessageSystem : MessageSystem<EntityId, MISSION_MESSAGE>
{
}
```

정본 규칙:
- `ownerKey`는 외부 구독 GameObject의 `EntityId`다.
- MissionManager는 `mMessageSystem`을 field initializer에서 즉시 생성한다.
- MissionManager가 notify 발행 책임을 가진다.

---

## MISSION_MESSAGE Values

| Value | Purpose |
|---|---|
| `NONE` | 기본값 |
| `RUNTIME_INIT` | runtime 신규 생성 또는 저장 복원 직후 |
| `RUNTIME_PROGRESS` | progress 변경 직후 (level up 제외) |
| `RUNTIME_CLAIMABLE` | claim 가능 상태 알림 |
| `RUNTIME_REWARDED` | 보상 지급 직후, 저장 직전 |
| `DAY_RESET` | daily reset 직후 |
| `ACHIEVE_LEVEL_UP` | achievement level up 직후 |

정본 source:
- `input/Domains/Game/ENUM_MISSION.json`

---

## Notify Responsibility

- runtime 신규 생성 또는 저장 복원 시: `RUNTIME_INIT`
- `MissionManager.RefreshRuntimes()` 호출 시 현재 runtime 전체에 대해 `RUNTIME_INIT` 재발행
- runtime progress 변경 시: `RUNTIME_PROGRESS`
- runtime이 `CLAIMABLE` 상태면 반복 notify 가능: `RUNTIME_CLAIMABLE`
- claim 성공 후 RewardManager가 만든 `RewardData[]`를 포함해 저장 직전에: `RUNTIME_REWARDED`
- daily reset 시 글로벌 1회: `DAY_RESET`
- achievement level up 시: `ACHIEVE_LEVEL_UP`

MissionRuntime / MissionScheduler는 UI를 직접 다루지 않는다.
UI 알림 발행 책임은 MissionManager에 둔다.

---

## Callback Shape

MissionMessageSystem 주석에는 callback payload 형식을 아래처럼 명시한다.

```csharp
// Default:
// args[0] = MissionRuntimeBase runtime
// args[1+] = message-specific extra payload
//
// Exception:
// DAY_RESET = no args
```

정본 규칙:
- 기본적으로 `args[0]`에는 해당 mission runtime을 넘긴다.
- 예외적으로 `DAY_RESET`은 global 1회 이벤트라 no args로 보낸다.
- 추가 정보가 필요할 때만 `args[1]`부터 append 한다.
- 메시지별 추가 payload:
  - `RUNTIME_INIT`: 추가 payload 없음
  - `RUNTIME_PROGRESS`: 추가 payload 없음
  - `RUNTIME_CLAIMABLE`: 추가 payload 없음
  - `RUNTIME_REWARDED`: `args[1] = RewardData[] rewards`
  - `DAY_RESET`: no args
  - `ACHIEVE_LEVEL_UP`: 추가 payload 없음

메시지별 의미:
- `RUNTIME_INIT`: 생성되었거나 복원된 mission runtime 전달
- `RUNTIME_PROGRESS`: progress 변경 전용. level up은 포함하지 않음
- `RUNTIME_CLAIMABLE`: 반복 notify 허용
- `RUNTIME_REWARDED`: reward apply 직후, save 직전에 발행
- `DAY_RESET`: global 1회 이벤트

---

## Related

- [22-message-system](../../20-domain-common-system/25-message-system/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
