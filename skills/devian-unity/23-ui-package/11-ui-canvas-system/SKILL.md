# UI Canvas System

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

Unity UI를 위한 UICanvas / UIPanel / UIBaseContainer 기본 구조를 제공한다.
Canvas owner, UI 기능 단위(Frame), Container의 초기화 수명주기를 표준화한다.

### Terms

| Term | Definition |
|------|------------|
| **UICanvas** | 비제네릭 canvas owner base. Canvas 캐시, Init 흐름, Validate, dynamic container registration을 담당한다 |
| **UICanvas\<TCanvas\>** | 타입 안전 singleton layer. `static Instance`를 제공하며 `where TCanvas : UICanvas` 제약을 가진다 |
| **UIPanel** | Canvas 하위 UI 기능 단위의 비제네릭 기반 클래스. _InitFromCanvas(MonoBehaviour) 진입점 제공 |
| **UIPanel\<TCanvas\>** | 타입 안전 버전. 강타입 owner 참조 + onInit(TCanvas) 확장점 제공 |
| **UIBaseContainer** | Container 기반 클래스. UICanvas.Init()에서 자동 수집되어 Container → Frame → Canvas 순서로 초기화 |
| **UIBaseFrame** | Container 내부 하위 요소의 공통 기반 클래스. scroll 상태는 소유하지 않으며, scroll 전용 계약은 별도 인터페이스로 분리 |

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
| **MUST** | UIPanel.Awake()는 non-virtual |
| **MUST** | `_InitFromCanvas()` / `_Init()` 중복 호출 방지 (initialized 가드) |
| **MUST** | `UIBaseContainer._InitComplete()`는 idempotent 해야 한다 |
| **MUST** | Init 순서: Container.onInit → Frame.onInit → Canvas.onInit |
| **MUST** | InitComplete 순서: Container.onInitComplete → Frame.onInitComplete → Canvas.onInitComplete |
| **MUST** | 동적 container subtree는 owner canvas가 `RegisterDynamicContainerTree()`로 편입한다 |

### Prohibited Actions

| Action | Reason |
|--------|--------|
| UIPanel.`Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| UICanvas.Awake()/OnDestroy() override | non-virtual — onAwake()/onDestroy() 사용 |
| `BaseUIFrame` 이름 사용 | `UIPanel`로 변경됨 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

---

## SSOT

### Canonical Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/
├── UICanvas.cs
├── UIPanel.cs
├── UIManager.cs
├── UIMessageSystem.cs
└── Container/
    ├── UIBaseContainer.cs
    └── UIBaseFrame.cs
```

### File List

| File | Purpose |
|------|---------|
| `UICanvas.cs` | UICanvas (비제네릭 base) + UICanvas\<T\> + BillboardMode + UICanvasInitPhase |
| `UIPanel.cs` | UIPanel + UIPanel\<TCanvas\> 클래스 |
| `UIBaseContainer.cs` | UIBaseContainer 추상 클래스 |
| `UIBaseFrame.cs` | UIBaseFrame 추상 클래스 (Container 내부 하위 요소) |
| `UIUtils.cs` | 공용 static 유틸리티 (좌표 변환, Billboard, Cursor) — `50-ui-utils` 참조 |

### Public API Signatures

#### UIBaseContainer (신규)

```csharp
namespace Devian
{
    public abstract class UIBaseContainer : MonoBehaviour, IPoolable
    {
        public bool isContainerInitialized { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        // Internal (UICanvas가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Override points
        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();

        // IPoolable
        public virtual void OnPoolSpawned();
        public virtual void OnPoolDespawned();
    }
}
```

#### UIBaseFrame

```csharp
namespace Devian
{
    public abstract class UIBaseFrame : MonoBehaviour
    {
        public bool isFrameInitialized { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        // Internal (UIBaseContainer가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Size (virtual — Grid 등 동적 크기 하위 클래스에서 override)
        public virtual float GetWidth();
        public virtual float GetHeight();

        // Scroll 전용 계약은 IUIScrollSection 인터페이스로 분리됨
        // (Show/Hide/Refresh 없음 — scroll 정보를 소유하지 않음)

        internal virtual void _Clear();

        // Override points
        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();
    }
}
```

#### UICanvas (비제네릭 base)

```csharp
namespace Devian
{
    public abstract class UICanvas : MonoBehaviour
    {
        public Canvas canvas { get; }
        public bool isInitialized { get; }
        public bool isInitComplete { get; }

        public void Init();
        public virtual bool Validate(out string reason);

        internal void RegisterDynamicContainerTree(UIBaseContainer root);

        protected virtual void onAwake();
        protected virtual void onDestroy();
        protected virtual void onInit();
        protected virtual void onInitComplete();
    }
}
```

#### UICanvas\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UICanvas<TCanvas> : UICanvas
        where TCanvas : UICanvas
    {
        public static TCanvas Instance { get; }
    }
}
```

#### UIPanel (비제네릭)

```csharp
namespace Devian
{
    public abstract class UIPanel : MonoBehaviour
    {
        public bool isInitialized { get; }
        public RectTransform rectTransform { get; }
        protected MonoBehaviour ownerBase { get; }
        protected UICanvas ownerCanvas { get; }

        protected void Awake();
        protected virtual void onAwake();

        internal void _InitFromCanvas(MonoBehaviour owner);
        internal void _InitComplete();

        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete();

        public T CreateContainer<T>(string prefabName, Transform parent = null)
            where T : UIBaseContainer;
    }
}
```

#### UIPanel\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIPanel<TCanvas> : UIPanel
        where TCanvas : UICanvas
    {
        public TCanvas owner { get; }

        protected sealed override void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInit(TCanvas owner);
    }
}
```

### Initialization Sequence (Canonical)

```
UICanvas.Init()
├── 1. if (isInitialized) return
├── 2. isInitialized = true
├── 3. mContainers = GetComponentsInChildren<UIBaseContainer>(true)
├── 4. mPanels = GetComponentsInChildren<UIPanel>(true)
│
├── Phase 1: Container Init
│   └── foreach plugin: plugin._Init()          → onInit()
│
├── Phase 2: Frame Init
│   └── foreach panel: panel._InitFromCanvas(this)  → onInit(TCanvas)
│
├── Phase 3: Canvas Init
│   └── onInit()
│
├── Phase 4: Container InitComplete
│   ├── foreach container: container._InitComplete()
│   └── FlushPendingDynamicContainers()
│
├── Phase 5: Panel InitComplete
│   └── foreach panel: panel._InitComplete()
│
├── Phase 6: Canvas InitComplete
│   └── onInitComplete()
│
└── Phase 7: Notify
    └── UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)
```

### Dynamic Container Registration

`UIPanel.CreateContainer<T>()`는 `BundlePool.Spawn<T>()` 후
`ownerCanvas.RegisterDynamicContainerTree(root)`에 위임한다.

`RegisterDynamicContainerTree()`의 책임:

- root 이하 `UIBaseContainer` subtree 수집
- 아직 초기화되지 않은 container에 즉시 `_Init(canvas)` 호출
- `canvas.Init()` 완료 전이면 pending queue 등록
- `canvas.Init()` 완료 후면 `_InitComplete()` 즉시 호출

이 구조로 `panel.onInit()` 내부와 일반 런타임 시점 모두 지원한다.

### Singleton Behavior

| 항목 | 설명 |
|------|------|
| **상속** | `MonoBehaviour` 직접 상속 |
| **Instance** | `static TCanvas Instance` — Awake에서 설정, OnDestroy에서 클린업 |
| **DontDestroyOnLoad** | 미적용 — 씬 전환 시 자동 파괴 |

### Type Constraints

| Generic | Constraint |
|---------|------------|
| `UICanvas<TCanvas>` | `where TCanvas : UICanvas` |
| `UIPanel<TCanvas>` | `where TCanvas : UICanvas` |

---

## DoD (Definition of Done) Checklist

### Files Exist
- [ ] `UICanvas.cs`
- [ ] `UIPanel.cs`
- [ ] `UIBaseContainer.cs` (신규)
- [ ] `UICanvas` 비제네릭 base + `UICanvas<TCanvas>` 제네릭 layer가 같은 파일에 존재

### Naming
- [ ] `BaseUIFrame` 문자열이 코드에 0건
- [ ] 모든 타입이 `namespace Devian { }` 내에 선언됨
- [ ] internal 메서드가 `_` 접두어 (`_Init`, `_InitFromCanvas`, `_InitComplete`)
- [ ] protected 메서드가 lowerCamelCase (`onInit`, `onInitComplete`)

### Init Order
- [ ] Container.onInit() → Frame.onInit() → Canvas.onInit()
- [ ] Container.onInitComplete() → Frame.onInitComplete() → Canvas.onInitComplete()
- [ ] 중복 _Init() 방지 (isContainerInitialized 가드)
- [ ] 중복 _InitComplete() 방지 (idempotent guard)
- [ ] 중복 _InitFromCanvas() 방지 (isInitialized 가드)
- [ ] 동적 container subtree가 owner canvas lifecycle에 편입됨

### Build
- [ ] 컴파일 오류 0개

---

## Reference

- **Singleton**: [20-common-package/29-singleton/SKILL.md](../../20-common-package/29-singleton/SKILL.md)
- **Pool System**: [20-common-package/27-pool-system/SKILL.md](../../20-common-package/27-pool-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
- **UIScrollContainer / IUIScrollSection**: [22-ui-container-scroll/SKILL.md](../22-ui-container-scroll/SKILL.md)
