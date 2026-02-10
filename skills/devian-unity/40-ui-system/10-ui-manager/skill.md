# UIManager

## Overview

UI Canvas의 수명주기를 관리하는 중앙 매니저.
Canvas 조회, 생성, 보장 및 유틸리티 기능을 제공한다.

**AutoSingleton** 기반. `Instance` 접근 시 자동 생성된다.

---

## Scope

### Includes
- UI 메시지 시스템 (`messageSystem`)
- Canvas 조회 (`TryGetCanvas`)
- Canvas 생성 (`CreateCanvas`)
- Canvas 보장 (`EnsureCanvas`)
- Canvas 제거 (`DespawnCanvas`)
- Canvas 검증 (`ValidateCanvas`)
- 커서 설정 (`SetCursor`)

### Excludes
- EventSystem 보장/생성 (UIManager는 EventSystem 보장 로직을 갖지 않는다)
- 게임플레이 입력 (ActionMap, 리바인딩, 컨텍스트 전환)
- InputActions 자산/바인딩 정책
- 언어/로컬라이제이션
- CreateComponent (컴포넌트 생성)
- 화면 스택/라우팅/네비게이션

---

## SSOT

### Code Path
```
framework-cs/upm/com.devian.ui/Runtime/UIManager.cs
```

### Class
```csharp
namespace Devian
{
    public sealed class UIManager : AutoSingleton<UIManager>
}
```

### Singleton Type
- **AutoSingleton** — `Instance` 접근 시 자동 생성
- Bootstrap 부착 불필요

---

## API

### messageSystem

```csharp
public static UIMessageSystem messageSystem => Instance?.mMessageSystem;
```

- 정적 접근: `UIManager.messageSystem`
- UI 레벨 메시징 인스턴스 (`MessageSystem<UnityEngine.EntityId, UI_MESSAGE>` 특화)
- UIManager 생성 시 내부에서 `new UIMessageSystem()` 초기화
- UI 메시지(InitOnce, ReloadText, Resize 등)의 발행/구독에 사용
- Shutdown 시 `Instance`가 `null`을 반환하면 `messageSystem`도 `null` (NRE 방지)

```csharp
UIManager.messageSystem.Subcribe(ownerEntityId, UI_MESSAGE.ReloadText, args => { /* ... */ return false; });
```

### TryGetCanvas

```csharp
public bool TryGetCanvas<TCanvas>(out TCanvas canvas)
    where TCanvas : MonoBehaviour
```

- Singleton 레지스트리 우선 조회
- 없으면 `FindObjectOfType<TCanvas>(true)`로 씬 탐색
- 찾으면 `true`, 없으면 `false`

### CreateCanvas

```csharp
public TCanvas CreateCanvas<TCanvas>(string prefabName, Transform parent = null)
    where TCanvas : MonoBehaviour, IPoolable<TCanvas>
```

- `BundlePool.Spawn<TCanvas>(prefabName, parent: parent)` 사용
- **중복 처리 정책**: 스폰 후 기존 싱글톤이 존재하고 새 인스턴스와 다르면:
  - 새 인스턴스를 `BundlePool.Despawn()`
  - 기존 인스턴스 반환
- 이유: Singleton Canvas는 "타입당 1개" 원칙

### EnsureCanvas

```csharp
public TCanvas EnsureCanvas<TCanvas>(string prefabName, Transform parent = null)
    where TCanvas : MonoBehaviour, IPoolable<TCanvas>
```

- `TryGetCanvas` 성공 시 기존 반환
- 실패 시 `CreateCanvas` 호출

### DespawnCanvas

```csharp
public void DespawnCanvas<TCanvas>()
    where TCanvas : MonoBehaviour
```

- `TryGetCanvas`로 조회 후 `BundlePool.Despawn()` 호출
- 주의: 풀링 대상이 아닌 Canvas는 직접 `Destroy()` 사용 권장

### ValidateCanvas

```csharp
public bool ValidateCanvas<TCanvas>(out string reason)
    where TCanvas : UICanvas<TCanvas>
```

- `TryGetCanvas` 실패 시 `reason = "Canvas not found"`, `false` 반환
- 성공 시 `canvas.Validate(out reason)` 결과 반환

### SetCursor

```csharp
public void SetCursor(bool visible, CursorLockMode lockMode)
```

- `Cursor.visible = visible`
- `Cursor.lockState = lockMode`

---

## Policies

### Naming
C# 메서드 네이밍(internal `_` 접두어, protected lowerCamelCase)은 상위 Devian 네이밍 정책을 준수한다.

### Duplicate Handling (Canvas)
`CreateCanvas`가 중복 생성되면 새 인스턴스를 despawn하고 기존 인스턴스를 반환한다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `AutoSingleton<T>` | `Runtime/Unity/Singletons/AutoSingleton.cs` |
| `BundlePool` | `Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `UICanvas<T>` | `com.devian.ui/Runtime/UICanvas.cs` |
| `UIMessageSystem` | `com.devian.ui/Runtime/UIMessageSystem.cs` |
| `Singleton` | `Runtime/Unity/Singletons/Singleton.cs` |

---

## Related Documents

- [UICanvas/UIFrame](../20-ui-canvas-frames/skill.md)
- [UIMessageSystem](../33-ui-message-system/skill.md)
- [Singleton](../../10-base-system/31-singleton/SKILL.md)
- [Pool Factories](../../10-base-system/04-pool-factories/SKILL.md)
