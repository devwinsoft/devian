# UIManager

Status: ACTIVE
AppliesTo: v11

---

## Overview

UI Canvas 수명주기 진입점과 UI 메시지 시스템을 제공하는 중앙 매니저.
`AutoSingleton<UIManager>` 기반이며 `Instance` 접근 시 script-created 된다.

---

## Scope

### Includes
- UI 메시지 시스템 소유 (`messageSystem`)
- Canvas 조회 (`TryGetCanvas`)
- Canvas 생성 (`CreateCanvas`)
- Canvas 제거 (`DespawnCanvas`)
- Canvas 검증 (`ValidateCanvas`)

### Excludes
- 커서 설정 (`UIUtils.SetCursor` 사용)
- EventSystem 보장/생성
- 게임플레이 입력 (ActionMap, 리바인딩, 컨텍스트 전환)
- 화면 스택/라우팅/네비게이션
- 로컬라이제이션 정책

---

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/UIManager.cs
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
- scene/prefab 사전 부착 금지

---

## API

### messageSystem

```csharp
public static UIMessageSystem messageSystem => Instance?.mMessageSystem;
```

- 정적 접근: `UIManager.messageSystem`
- `UIManager` 내부 필드 `mMessageSystem`을 노출한다
- `Instance` 접근이 `AutoSingleton` 생성을 트리거하므로 첫 접근 시 메시지 시스템도 준비된다
- shutdown 중 `Instance`가 `null`을 반환할 수 있으므로 프로퍼티도 nullable 동작을 가진다

### TryGetCanvas

```csharp
public bool TryGetCanvas<TCanvas>(out TCanvas canvas)
    where TCanvas : MonoBehaviour
```

- `Singleton.TryGet<TCanvas>`를 먼저 조회한다
- 없으면 `FindAnyObjectByType<TCanvas>(FindObjectsInactive.Include)`로 씬 전체를 탐색한다
- inactive object도 탐색 대상이다

### CreateCanvas

```csharp
public TCanvas CreateCanvas<TCanvas>(string prefabName, Transform parent = null)
    where TCanvas : MonoBehaviour, IPoolable
```

- `BundlePool.Spawn<TCanvas>(prefabName, parent: parent)`로 생성한다
- 생성 직후 같은 타입의 기존 singleton canvas가 이미 있으면:
  새 인스턴스를 `BundlePool.Despawn()`하고 기존 인스턴스를 반환한다
- 타입당 1개 singleton canvas 정책을 강제하기 위한 duplicate collapse 동작이다

### DespawnCanvas

```csharp
public void DespawnCanvas<TCanvas>()
    where TCanvas : MonoBehaviour
```

- `TryGetCanvas` 성공 시 `BundlePool.Despawn(canvas)` 호출
- poolable canvas를 대상으로 사용하는 API다

### ValidateCanvas

```csharp
public bool ValidateCanvas<TCanvas>(out string reason)
    where TCanvas : UICanvas<TCanvas>
```

- canvas를 찾지 못하면 `"Canvas of type {TCanvas} not found"` 형식 reason을 반환한다
- 찾으면 `canvas.Validate(out reason)`를 그대로 위임한다

---

## Policies

### Duplicate Handling
`CreateCanvas`는 중복 생성 시 새 인스턴스를 버리고 기존 singleton 인스턴스를 반환한다.

### Responsibility Boundary
UIManager는 canvas lifecycle/service entry만 담당한다.
UI utility (`SetCursor`, billboard, 좌표 변환)는 `UIUtils`로 분리되어 있다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `AutoSingleton<T>` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Singletons/AutoSingleton.cs` |
| `Singleton` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Singletons/Singleton.cs` |
| `BundlePool` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/IPoolable.cs` |
| `UICanvas<TCanvas>` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/UICanvas.cs` |
| `UIMessageSystem` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/UIMessageSystem.cs` |

---

## Related Documents

- [UICanvas/UIPanel/UIBaseContainer](../11-ui-canvas-system/SKILL.md)
- [UIMessageSystem](../33-ui-message-system/SKILL.md)
- [UIUtils](../50-ui-utils/SKILL.md)
- [Singleton](../../20-common-package/29-singleton/SKILL.md)
- [Pool System](../../20-common-package/27-pool-system/SKILL.md)
