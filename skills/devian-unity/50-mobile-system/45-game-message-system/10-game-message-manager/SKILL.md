# 10-game-message-manager

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 `GameMessageManager` 설계 문서다.

---

## Purpose

`GameMessageManager`는 메시지/트리거 계층 리팩토링의 진입점 클래스다.

현재 역할:
- Game message trigger 라우터를 캡슐화한다.
- `GameMessageStorage`를 소유하고 `message.stats`를 관리한다.
- `NotifyGameMessage`에서 `message.stats`를 선갱신한다.
- game trigger publish 후 `AchieveManager.Notify`를 호출한다.

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Message/GameMessageManager.cs`
- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Message/GameMessageStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Message/GameMessageManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Message/GameMessageStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Message/GameMessageManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Message/GameMessageStorage.cs`

---

## Phase-1 API (요약)

- `GameMessageStorage Storage { get; }`
- `TryGetStat(string messageId, out CBigInt)`
- `GetStat(string messageId) / SetStat(string messageId, CBigInt)`
- `ClearStorage()`
- `NotifyGameMessage(GAME_MESSAGE_TYPE, CBigInt)` (stats update + publish + achieve notify)
- `SubcribeGameMessageTrigger(...) / UnSubcribeGameMessageTrigger(...)` (internal helper)
- `ClearAll()`

주의:
- game message trigger 인스턴스는 외부에 직접 노출하지 않는다.
- MissionManager는 `Notify`를 GameMessageManager로 위임한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-game-message-trigger](../11-game-message-trigger/SKILL.md)
- [14-game-message-storage](../14-game-message-storage/SKILL.md)
- [48-mission-system/16-mission-message-trigger](../../48-mission-system/16-mission-message-trigger/SKILL.md)
- [48-mission-system/10-mission-manager](../../48-mission-system/10-mission-manager/SKILL.md)
