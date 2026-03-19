# UI Canvas System

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

Unity UI를 위한 UICanvas / UICanvasFrame / UIContainerBase 기본 구조를 제공한다.
Canvas owner, UI 기능 단위(Frame), Container의 초기화 수명주기를 표준화한다.

### Terms

| Term | Definition |
|------|------------|
| **UICanvas** | Canvas owner. 씬 종속 MonoBehaviour 싱글톤으로, Init() 호출 시 Container → Frame → Canvas 순서로 초기화. 씬 전환 시 자동 파괴됨 (DontDestroyOnLoad 미적용) |
| **UICanvasFrame** | Canvas 하위 UI 기능 단위의 비제네릭 기반 클래스. _InitFromCanvas(MonoBehaviour) 진입점 제공 |
| **UICanvasFrame\<TCanvas\>** | 타입 안전 버전. 강타입 owner 참조 + onInit(TCanvas) 확장점 제공 |
| **UIContainerBase** | Container 기반 클래스. UICanvas.Init()에서 자동 수집되어 Container → Frame → Canvas 순서로 초기화 |
| **UIContainerFrameBase** | Container 내부 하위 요소의 공통 기반 클래스. scroll 상태는 소유하지 않으며, scroll 전용 계약은 별도 인터페이스로 분리 |

---

## Policy

### Namespace Policy

| Rule | Description |
|------|-------------|
| **MUST** | `namespace Devian` 단일 루트 네임스페이스만 사용 |

### Lifecycle Policy

| Rule | Description |
|------|-------------|
| **MUST** | `Awake()` → `onAwake()` 패턴 사용 |
| **MUST** | UICanvas.Awake()는 non-virtual. Instance 설정 + canvas 캐시 + onAwake() |
| **MUST** | UICanvas.OnDestroy()는 non-virtual. `!IsApplicationQuitting` 가드 |
| **MUST** | UICanvasFrame.Awake()는 non-virtual |
| **MUST** | `_InitFromCanvas()` / `_Init()` 중복 호출 방지 (initialized 가드) |
| **MUST** | Init 순서: Container.onInit → Frame.onInit → Canvas.onInit |
| **MUST** | InitComplete 순서: Container.onInitComplete → Frame.onInitComplete → Canvas.onInitComplete |

### Prohibited Actions

| Action | Reason |
|--------|--------|
| UICanvasFrame.`Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| UICanvas.Awake()/OnDestroy() override | non-virtual — onAwake()/onDestroy() 사용 |
| `BaseUIFrame` 이름 사용 | `UICanvasFrame`로 변경됨 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

---

## SSOT

### Canonical Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/
├── UICanvas.cs
├── UICanvasFrame.cs
├── UIManager.cs
├── UIMessageSystem.cs
└── Container/
    ├── UIContainerBase.cs
    └── UIContainerFrameBase.cs
```

### File List

| File | Purpose |
|------|---------|
| `UICanvas.cs` | UICanvas\<T\> 추상 클래스 + BillboardMode enum |
| `UICanvasFrame.cs` | UICanvasFrame + UICanvasFrame\<TCanvas\> 클래스 |
| `UIContainerBase.cs` | UIContainerBase 추상 클래스 |
| `UIContainerFrameBase.cs` | UIContainerFrameBase 추상 클래스 (Container 내부 하위 요소) |
| `UIUtils.cs` | 공용 static 유틸리티 (좌표 변환, Billboard, Cursor) — `50-ui-utils` 참조 |

### Public API Signatures

#### UIContainerBase (신규)

```csharp
namespace Devian
{
    public abstract class UIContainerBase : MonoBehaviour
    {
        public bool isContainerInitialized { get; }
        public Canvas canvas { get; }

        // Internal (UICanvas가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Override points
        protected virtual void onInit();
        protected virtual void onInitComplete();
    }
}
```

#### UIContainerFrameBase

```csharp
namespace Devian
{
    public abstract class UIContainerFrameBase : MonoBehaviour
    {
        public bool isFrameInitialized { get; }
        public Canvas canvas { get; }

        // Internal (UIContainerBase가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Size (virtual — Grid 등 동적 크기 하위 클래스에서 override)
        public virtual float GetWidth();
        public virtual float GetHeight();

        // Scroll 전용 계약은 IScrollSection 인터페이스로 분리됨
        // (Show/Hide/Refresh 없음 — scroll 정보를 소유하지 않음)

        internal virtual void _Clear();

        // Override points
        protected virtual void onInit();
        protected virtual void onInitComplete();
    }
}
```

#### UICanvasFrame (비제네릭)

```csharp
namespace Devian
{
    public abstract class UICanvasFrame : MonoBehaviour
    {
        public bool isInitialized { get; }
        protected MonoBehaviour ownerBase { get; }

        protected void Awake();
        protected virtual void onAwake();

        internal void _InitFromCanvas(MonoBehaviour owner);
        internal void _InitComplete();

        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete();
    }
}
```

#### UICanvasFrame\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UICanvasFrame<TCanvas> : UICanvasFrame
        where TCanvas : MonoBehaviour
    {
        public TCanvas owner { get; }

        protected sealed override void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInit(TCanvas owner);
    }
}
```

#### UICanvas\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UICanvas<TCanvas> : MonoBehaviour
        where TCanvas : MonoBehaviour
    {
        public static TCanvas Instance { get; }
        public Canvas canvas { get; }

        protected void Awake();
        protected void OnDestroy();
        protected virtual void onAwake();
        protected virtual void onDestroy();
        protected virtual void onInit();
        protected virtual void onInitComplete();

        public void Init();
        public virtual bool Validate(out string reason);
        public FRAME CreateFrame<FRAME>(string prefabName, Transform parent = null)
            where FRAME : Component, IPoolable;

        // Helpers → UIUtils로 이동됨 (50-ui-utils 참조)
    }
}
```

### Initialization Sequence (Canonical)

```
UICanvas.Init()
├── 1. if (mInitialized) return
├── 2. mInitialized = true
├── 3. mContainers = GetComponentsInChildren<UIContainerBase>(true)
├── 4. mFrames = GetComponentsInChildren<UICanvasFrame>(true)
│
├── Phase 1: Container Init
│   └── foreach plugin: plugin._Init()          → onInit()
│
├── Phase 2: Frame Init
│   └── foreach frame: frame._InitFromCanvas(this)  → onInit(TCanvas)
│
├── Phase 3: Canvas Init
│   └── onInit()
│
├── Phase 4: InitComplete (Container → Frame → Canvas)
│   ├── foreach plugin: plugin._InitComplete()   → onInitComplete()
│   ├── foreach frame: frame._InitComplete()     → onInitComplete()
│   └── onInitComplete()
│
└── Phase 5: Notify
    └── UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)
```

### Singleton Behavior

| 항목 | 설명 |
|------|------|
| **상속** | `MonoBehaviour` 직접 상속 |
| **Instance** | `static TCanvas Instance` — Awake에서 설정, OnDestroy에서 클린업 |
| **DontDestroyOnLoad** | 미적용 — 씬 전환 시 자동 파괴 |

### Type Constraints

| Generic | Constraint |
|---------|------------|
| `UICanvas<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `UICanvasFrame<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `CreateFrame<FRAME>` | `where FRAME : Component, IPoolable` |

### Dependencies

| Dependency | Location |
|------------|----------|
| `BundlePool` | `com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable` | `com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/IPoolable.cs` |

---

## DoD (Definition of Done) Checklist

### Files Exist
- [ ] `UICanvas.cs`
- [ ] `UICanvasFrame.cs`
- [ ] `UIContainerBase.cs` (신규)

### Naming
- [ ] `BaseUIFrame` 문자열이 코드에 0건
- [ ] 모든 타입이 `namespace Devian { }` 내에 선언됨
- [ ] internal 메서드가 `_` 접두어 (`_Init`, `_InitFromCanvas`, `_InitComplete`)
- [ ] protected 메서드가 lowerCamelCase (`onInit`, `onInitComplete`)

### Init Order
- [ ] Container.onInit() → Frame.onInit() → Canvas.onInit()
- [ ] Container.onInitComplete() → Frame.onInitComplete() → Canvas.onInitComplete()
- [ ] 중복 _Init() 방지 (isContainerInitialized 가드)
- [ ] 중복 _InitFromCanvas() 방지 (isInitialized 가드)

### Build
- [ ] 컴파일 오류 0개

---

## Reference

- **Singleton**: [20-common-package/29-singleton/SKILL.md](../../20-common-package/29-singleton/SKILL.md)
- **Pool System**: [20-common-package/27-pool-system/SKILL.md](../../20-common-package/27-pool-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
- **UIContainerScrollView / IScrollSection**: [22-ui-container-scroll-view/SKILL.md](../22-ui-container-scroll-view/SKILL.md)
