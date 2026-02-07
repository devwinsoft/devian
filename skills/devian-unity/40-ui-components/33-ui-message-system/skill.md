# UIMessageSystem

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

UI 전용 메시지 시스템. `MessageSystem<EntityId, UI_MESSAGE>`를 특화한 인스턴스 클래스이다.
`UIManager.messageSystem` 정적 프로퍼티로 단일 인스턴스에 접근하며,
UI 레벨 이벤트(초기화 완료, 텍스트 리로드, 리사이즈 등)를 ownerKey 기반으로 발행/구독할 수 있다.

### Terms

| Term | Definition |
|------|------------|
| **UIMessageSystem** | `MessageSystem<EntityId, UI_MESSAGE>` 특화 클래스 |
| **UI_MESSAGE** | UI 메시지 키 enum (`None`, `InitOnce`, `ReloadText`, `Resize`) |
| **EntityId** | `UnityEngine.EntityId`. ownerKey로 사용 (`GetEntityId()` 반환값) |

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.ui/Runtime/UIMessageSystem.cs
```

### Source

```csharp
namespace Devian
{
    public enum UI_MESSAGE
    {
        None,
        InitOnce,
        ReloadText,
        Resize,
    }

    public class UIMessageSystem : MessageSystem<EntityId, UI_MESSAGE>
    {
    }
}
```

### Owner

`UIManager.messageSystem` 정적 프로퍼티가 유일한 인스턴스를 소유한다.

```csharp
// UIManager.cs
private UIMessageSystem mMessageSystem = new UIMessageSystem();
public static UIMessageSystem messageSystem => Instance.mMessageSystem;
```

---

## API

`MessageSystem<TOwnerKey, TMsgKey>` 인스턴스 API를 그대로 상속한다.

| Method | Description |
|--------|-------------|
| `Subcribe(EntityId owner, UI_MESSAGE key, Handler handler)` | 메시지 핸들러 등록 |
| `SubcribeOnce(EntityId owner, UI_MESSAGE key, Action<object[]> handler)` | 1회성 핸들러 등록 |
| `UnSubcribe(EntityId owner)` | owner에 등록된 모든 핸들러 제거 |
| `Notify(UI_MESSAGE key, params object[] args)` | 메시지 발행 |
| `ClearAll()` | 전체 핸들러 초기화 |

### 사용 예시

```csharp
// register
UIManager.messageSystem.Subcribe(ownerEntityId, UI_MESSAGE.ReloadText, args => { /* ... */ return false; });

// subscribe once
UIManager.messageSystem.SubcribeOnce(ownerEntityId, UI_MESSAGE.InitOnce, args => { /* ... */ });

// notify (no owner)
UIManager.messageSystem.Notify(UI_MESSAGE.ReloadText);

// unsubscribe
UIManager.messageSystem.UnSubcribe(ownerEntityId);
```

### UI_MESSAGE Values

| Value | Purpose |
|-------|---------|
| `None` | 기본값 (사용하지 않음) |
| `InitOnce` | UICanvas.Init() 완료 시 1회 발행 |
| `ReloadText` | 텍스트 리로드 요청 (언어 변경 등) |
| `Resize` | UI 리사이즈 통지 |

### Notify 시점

`UICanvas.Init()`이 완료될 때 `UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)`가 호출된다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `MessageSystem<TOwnerKey, TMsgKey>` | `com.devian.foundation/Runtime/Unity/MessageSystem/MessageSystem.cs` |
| `UnityEngine.EntityId` | Unity 내장 (`UnityEngine.Object.GetEntityId()` 반환 타입) |
| `UIManager` | `com.devian.ui/Runtime/UIManager.cs` |

---

## Reference

- **MessageSystem**: [03-message-system/SKILL.md](../../30-unity-components/03-message-system/SKILL.md)
- **UIManager**: [10-ui-manager/skill.md](../10-ui-manager/skill.md)
- **EntityId**: `UnityEngine.EntityId` (Unity 내장)
