# UIManager

## Overview

UI Canvas의 수명주기를 관리하고 UI 입력 인프라를 보장하는 중앙 매니저.
Canvas 조회, 생성, 보장, EventSystem/InputModule 보장 및 유틸리티 기능을 제공한다.

**Bootstrap에 부착되는 CompoSingleton**이다. 런타임 자동 생성되지 않는다.

---

## Scope

### Includes
- Canvas 조회 (`TryGetCanvas`)
- Canvas 생성 (`CreateCanvas`)
- Canvas 보장 (`EnsureCanvas`)
- Canvas 제거 (`DespawnCanvas`)
- Canvas 검증 (`ValidateCanvas`)
- 커서 설정 (`SetCursor`)
- EventSystem 보장 (`EnsureUiEventSystem`)
- InputSystemUIInputModule 보장
- StandaloneInputModule 제거

### Excludes
- 게임플레이 입력 (ActionMap, 리바인딩, 컨텍스트 전환)
- InputActions 자산/바인딩 정책
- 언어/로컬라이제이션
- CreateComponent (컴포넌트 생성)
- 화면 스택/라우팅/네비게이션

---

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Runtime/Unity/UI/UIManager.cs
```

### Class
```csharp
namespace Devian
{
    public sealed class UIManager : CompoSingleton<UIManager>
}
```

### Singleton Type
- **CompoSingleton** (Bootstrap에 부착)
- BaseBootstrap.Awake()에서 `ensureComponent<UIManager>()` 호출로 보장됨
- 런타임 자동 생성 없음 (AutoSingleton 아님)

---

## API

### EnsureUiEventSystem

```csharp
public void EnsureUiEventSystem()
```

UI 입력에 필요한 EventSystem과 InputSystemUIInputModule을 보장한다.
Awake에서 자동 호출되며, 수동 호출도 가능하다.

**동작:**
1. EventSystem 검색/생성
   - 0개: 새 GameObject 생성 후 UIManager 하위로 배치
   - 1개: 기존 인스턴스 재사용
   - 2개+: 첫 번째 사용, `Debug.LogWarning` (삭제 금지)
2. StandaloneInputModule 발견 시 `Destroy()`
3. InputSystemUIInputModule이 없으면 Reflection으로 추가

### TryGetCanvas

```csharp
public bool TryGetCanvas<TCanvas>(out TCanvas canvas)
    where TCanvas : MonoBehaviour
```

- CompoSingleton 레지스트리 우선 조회
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
- 이유: CompoSingleton Canvas는 "타입당 1개" 원칙

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

### EventSystem 중복 처리
EventSystem이 2개 이상 발견되면 `Debug.LogWarning`만 남기고 제거하지 않는다.

### InputSystemUIInputModule (Reflection)
- `using UnityEngine.InputSystem.UI;` 금지 (asmdef 의존성 유발)
- Reflection으로 타입 로드:
  1. `"UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"` (Assembly-qualified)
  2. `"UnityEngine.InputSystem.UI.InputSystemUIInputModule"` (Fallback)
- 타입을 못 찾으면 `Debug.LogError`만 (throw 금지)

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `CompoSingleton<T>` | `Runtime/Unity/Singletons/CompoSingleton.cs` |
| `BundlePool` | `Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `UICanvas<T>` | `Runtime/Unity/UI/UICanvas.cs` |
| `Singleton` | `Runtime/Unity/Singletons/Singleton.cs` |
| `BaseBootstrap` | `Runtime/Unity/Bootstrap/BaseBootstrap.cs` |
| `EventSystem` | `UnityEngine.EventSystems` |

---

## Related Documents

- [UICanvas/UIFrame](../20-ui-canvas-frames/skill.md)
- [Singleton](../../30-unity-components/31-singleton/SKILL.md)
- [Pool Factories](../../30-unity-components/04-pool-factories/SKILL.md)
- [Bootstrap](../../30-unity-components/27-bootstrap-resource-object/SKILL.md)
