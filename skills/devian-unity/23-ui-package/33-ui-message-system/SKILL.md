# UIMessageSystem

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

UI 전용 메시지 시스템.
`BaseTrigger<EntityId, UI_MESSAGE>`를 특화한 인스턴스 클래스이며,
`UIManager.messageSystem` 정적 프로퍼티를 통해 단일 인스턴스에 접근한다.

### Terms

| Term | Definition |
|------|------------|
| **UIMessageSystem** | `BaseTrigger<EntityId, UI_MESSAGE>` 특화 클래스 |
| **UI_MESSAGE** | UI 메시지 키 enum (`None`, `InitOnce`, `ReloadText`, `Resize`) |
| **EntityId** | `UnityEngine.EntityId`. ownerKey로 사용 (`GetEntityId()` 반환값) |

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/UIMessageSystem.cs
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

    public class UIMessageSystem : BaseTrigger<EntityId, UI_MESSAGE>
    {
    }
}
```

### Owner

`UIManager`가 메시지 시스템 인스턴스를 내부 필드로 소유한다.

```csharp
private UIMessageSystem mMessageSystem = new UIMessageSystem();
public static UIMessageSystem messageSystem => Instance?.mMessageSystem;
```

---

## API

`BaseTrigger<TOwnerKey, TMsgKey>` 인스턴스 API를 그대로 상속한다.

| Method | Description |
|--------|-------------|
| `Subcribe(EntityId owner, UI_MESSAGE key, Handler handler)` | 메시지 핸들러 등록 |
| `SubcribeOnce(EntityId owner, UI_MESSAGE key, Action<object[]> handler)` | 1회성 핸들러 등록 |
| `UnSubcribe(EntityId owner)` | owner에 등록된 모든 핸들러 제거 |
| `Notify(UI_MESSAGE key, params object[] args)` | 메시지 발행 |
| `ClearAll()` | 전체 핸들러 초기화 |

### UI_MESSAGE Values

| Value | Purpose |
|-------|---------|
| `None` | 기본값 |
| `InitOnce` | `UICanvas.Init()` 완료 후 1회 발행 |
| `ReloadText` | 텍스트 리로드 요청 |
| `Resize` | UI 리사이즈 통지 |

### Notify Timing

`UICanvas.Init()` 마지막 단계에서 `UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)`가 호출된다.

### Behavior Notes

- ownerKey 기반 메시지 시스템이다
- `BaseTrigger` 구현은 notify 중 `Subcribe / UnSubcribe / ClearAll` 재진입을 허용한다
- `UIComponentText`는 `InitOnce`와 `ReloadText`를 직접 사용하는 대표 사례다

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `BaseTrigger<TOwnerKey, TMsgKey>` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/BaseTrigger/BaseTrigger.cs` |
| `UIManager` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/UIManager.cs` |
| `UnityEngine.EntityId` | Unity 내장 |

---

## Reference

- **BaseTrigger**: [25-trigger/SKILL.md](../../20-common-package/25-trigger/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
