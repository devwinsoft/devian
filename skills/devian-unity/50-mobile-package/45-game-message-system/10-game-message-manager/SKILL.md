# 10-game-message-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `GameMessageManager` 설계 문서다.

---

## Purpose

`GameMessageManager`는 메시지/트리거 계층 리팩토링의 진입점 클래스다.

현재 역할:
- Game message trigger 라우터를 캡슐화한다.
- `GameMessageStorage`를 소유하고 `message.stats`를 관리한다.
- `Notify`에서 `TOTAL_*` 타입만 `message.stats`를 선갱신한다.
- game trigger publish만 수행하고, mission/achieve는 trigger 구독으로 처리한다.

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/GameMessage/GameMessageManager.cs`
- UPM (정본): `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/GameMessage/GameMessageStorage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/GameMessage/GameMessageManager.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/GameMessage/GameMessageStorage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/GameMessage/GameMessageManager.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/GameMessage/GameMessageStorage.cs`

---

## Initialization

- `Initialize()`는 `MobileApplication.onLoadCompletedAsync()`에서 호출된다.
- 서버와 별개로 독립적으로 동작하므로, 로그인 흐름(`LoginManager`)이 아닌 리소스 로딩 완료 시점에 초기화한다.

## Phase-1 API (요약)

- `GameMessageStorage Storage { get; }`
- `Initialize()`
- `TryGetStat(string messageId, out CBigInt)`
- `GetStat(string messageId) / SetStat(string messageId, CBigInt)`
- `ClearStorage()`
- `Notify(GAME_MESSAGE_TYPE, CBigInt/long/int)` (stats update + publish)
- `SubcribeGameMessageTrigger(...) / UnSubcribeGameMessageTrigger(...)` (internal helper)
- `ClearAll()`

주의:
- game message trigger 인스턴스는 외부에 직접 노출하지 않는다.
- MissionManager/AchieveManager는 `SubcribeGameMessageTrigger` helper를 통해 trigger를 구독한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-game-message-trigger](../11-game-message-trigger/SKILL.md)
- [14-game-message-storage](../14-game-message-storage/SKILL.md)
- [48-mission-system/16-mission-message-trigger](../../48-mission-system/16-mission-message-trigger/SKILL.md)
- [48-mission-system/10-mission-manager](../../48-mission-system/10-mission-manager/SKILL.md)
