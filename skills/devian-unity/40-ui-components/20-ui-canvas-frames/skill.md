# UI Canvas Frames

Status: ACTIVE
AppliesTo: v10

---

## Overview

### Purpose

Unity UI를 위한 UICanvas/UIFrame 기본 구조를 제공한다.
Canvas owner와 UI 기능 단위(Frame)의 초기화 수명주기를 표준화한다.

### Terms

| Term | Definition |
|------|------------|
| **UICanvas** | Canvas owner. CompoSingleton 기반 싱글톤으로, 자식 Frame들의 초기화 주체 |
| **UIFrame** | Canvas 하위 UI 기능 단위. UICanvas로부터 _InitFromCanvas 호출을 받아 초기화됨 |
| **UIFrameBase** | UIFrame의 비제네릭 기반 클래스. _InitFromCanvas(MonoBehaviour) 진입점 제공 |
| **UIFrame\<TCanvas\>** | 타입 안전 버전. 강타입 owner 참조 + onInit(TCanvas) 확장점 제공 |

### Usage Flow

```
1. Scene Setup
   └── UICanvas<MyCanvas> 컴포넌트를 Canvas GameObject에 배치
       └── 자식에 UIFrame<MyCanvas> 컴포넌트들 배치

2. Runtime Initialization (Automatic)
   └── UICanvas.Awake() (override from CompoSingleton)
       ├── base.Awake()                    ← Singleton 등록
       ├── canvas = GetComponent<Canvas>()
       ├── onAwake()                       ← custom logic
       └── initChildFrames()
           └── foreach child UIFrameBase → frame._InitFromCanvas(this)

3. UIFrame Lifecycle
   └── UIFrame.Awake() → onAwake()
   └── UIFrame._InitFromCanvas(owner)
       ├── ownerBase = owner
       ├── isInitialized = true
       └── onInitFromCanvas(owner)
           └── (UIFrame<TCanvas>) owner as TCanvas 캐스팅
               └── onInit(TCanvas owner)   ← 확장점

4. Dynamic Frame Creation (Optional)
   └── existingFrame.createFrame<MyFrame>("PrefabName")
       ├── BundlePool.Spawn<MyFrame>(...)
       └── newFrame._InitFromCanvas(ownerBase)
```

### Includes / Excludes

**Includes:**
- Init lifecycle (Awake → onAwake, _InitFromCanvas → onInitFromCanvas → onInit)
- createFrame for dynamic frame creation via BundlePool
- Validation helpers (Validate)
- Coordinate conversion helpers (TryWorldToOverlayLocal)
- Billboard helpers (ComputeBillboardRotation, ApplyBillboard)

**Excludes:**
- UI 라우팅 / 스택 / 네비게이션 시스템
- UI 애니메이션 시스템
- Canvas 설정 변경 API (renderMode, worldCamera는 Inspector에서 설정)
- 자동 billboard 컴포넌트 / 자동 업데이트
- ~~IUiCanvasOwner 인터페이스~~ (제거됨)

---

## Policy

### Namespace Policy

| Rule | Description |
|------|-------------|
| **MUST** | `namespace Devian` 단일 루트 네임스페이스만 사용 |
| **FAIL** | `namespace Devian.UI` 등 하위 네임스페이스 사용 시 |

### Naming Policy

C# 메서드 네이밍(internal `_` 접두어, protected lowerCamelCase)은 상위 Devian 네이밍 정책을 따른다.

### Interface Policy

| Rule | Description |
|------|-------------|
| **MUST** | IUiCanvasOwner 인터페이스 사용 금지 (제거됨) |
| **MUST** | Canvas→Frame 연결은 타입 캐스팅으로 처리 |

### Lifecycle Policy

| Rule | Description |
|------|-------------|
| **MUST** | `Awake()` → `onAwake()` 패턴 사용 |
| **MUST** | UICanvas.Awake()는 `override` + `base.Awake()` 호출 (CompoSingleton 상속) |
| **MUST** | UIFrame.Awake()는 non-virtual (MonoBehaviour 직접 상속) |
| **MUST** | UICanvas는 `onAwake()` 완료 후 child frame `_InitFromCanvas(this)` 수행 |
| **MUST** | UIFrameBase._InitFromCanvas()는 owner 저장만 수행 |
| **MUST** | 실제 초기화 로직은 `onInit(TCanvas owner)`에서 처리 |
| **MUST** | `_InitFromCanvas()` 중복 호출 방지 (isInitialized 체크) |

### Creation Policy

| Rule | Description |
|------|-------------|
| **MUST** | `UIFrameBase.createFrame<T>()` 는 `BundlePool`로 생성 |
| **MUST** | 생성 직후 `_InitFromCanvas(ownerBase)` 호출 |
| **MUST** | `createFrame`은 `_InitFromCanvas` 이전 호출 시 예외 발생 |

### Prohibited Actions

| Action | Reason |
|--------|--------|
| UIFrame.`Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| UICanvas.Awake()에서 `base.Awake()` 누락 | CompoSingleton 등록 실패 |
| IUiCanvasOwner 인터페이스 사용 | 제거됨, 타입 캐스팅 방식으로 대체 |
| 자동 billboard 컴포넌트 / 자동 업데이트 | helper 메서드만 제공 |
| Canvas 설정 변경 API (`UseSharedWorldCamera` 등) | Inspector에서 설정, 코드는 검증/헬퍼만 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

### Validation Requirements

| Check | Expected |
|-------|----------|
| `ScreenSpaceOverlay` + `worldCamera != null` | **FAIL** |
| `ScreenSpaceCamera` + `worldCamera == null` | **FAIL** |

---

## SSOT

### Canonical Code Path

```
framework-cs/upm/com.devian.foundation/Runtime/Unity/UI/
```

### File List

| File | Purpose |
|------|---------|
| `UICanvas.cs` | UICanvas<T> 추상 클래스 + BillboardMode enum |
| `UIFrame.cs` | UIFrameBase + UIFrame<TCanvas> 클래스 |

#### Removed Files

| File | Reason |
|------|--------|
| ~~`IUiCanvasOwner.cs`~~ | 인터페이스 제거됨, 타입 캐스팅 방식으로 대체 |

### Public API Signatures

#### BillboardMode

```csharp
namespace Devian
{
    public enum BillboardMode
    {
        Full,
        YOnly
    }
}
```

#### UICanvas\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UICanvas<TCanvas> : CompoSingleton<TCanvas>
        where TCanvas : MonoBehaviour
    {
        // Properties
        public Canvas canvas { get; }

        // Lifecycle (override from CompoSingleton)
        protected override void Awake();  // calls base.Awake() first
        protected virtual void onAwake();

        // Validation
        public virtual bool Validate(out string reason);

        // Helpers
        public bool TryWorldToOverlayLocal(
            RectTransform overlaySpace,
            Vector3 worldPos,
            out Vector2 overlayLocal);

        public Quaternion ComputeBillboardRotation(
            Vector3 targetWorldPos,
            BillboardMode mode = BillboardMode.Full);

        public void ApplyBillboard(
            Transform target,
            BillboardMode mode = BillboardMode.Full);

        // Private
        private void initChildFrames();
    }
}
```

#### UIFrameBase

```csharp
namespace Devian
{
    public abstract class UIFrameBase : MonoBehaviour
    {
        // Properties
        public bool isInitialized { get; }
        protected MonoBehaviour ownerBase { get; }

        // Lifecycle (non-virtual)
        protected void Awake();
        protected virtual void onAwake();

        // Initialization (internal - called by UICanvas)
        internal void _InitFromCanvas(MonoBehaviour owner);
        protected abstract void onInitFromCanvas(MonoBehaviour owner);

        // Frame Creation
        protected FRAME createFrame<FRAME>(string prefabName, Transform parent = null)
            where FRAME : Component, IPoolable<FRAME>;
    }
}
```

#### UIFrame\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIFrame<TCanvas> : UIFrameBase
        where TCanvas : MonoBehaviour
    {
        // Properties
        public TCanvas owner { get; }

        // Lifecycle
        protected sealed override void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInit(TCanvas owner);
    }
}
```

### Initialization Sequence (Canonical)

#### UICanvas Initialization

```
UICanvas.Awake()  (override from CompoSingleton)
├── 1. base.Awake()                      ← CompoSingleton 등록
├── 2. canvas = GetComponent<Canvas>()
├── 3. onAwake()                         ← override point
└── 4. initChildFrames()
    └── foreach UIFrameBase in children
        └── frame._InitFromCanvas(this)
```

#### UIFrame Initialization

```
UIFrame.Awake()
└── onAwake()                            ← override point

UIFrameBase._InitFromCanvas(owner)
├── 1. if (isInitialized) return         ← 중복 방지
├── 2. ownerBase = owner
├── 3. isInitialized = true
└── 4. onInitFromCanvas(owner)           ← abstract, 파생 클래스 구현

UIFrame<TCanvas>.onInitFromCanvas(owner)
├── 1. this.owner = owner as TCanvas
├── 2. if (this.owner == null) error + return
└── 3. onInit(this.owner)                ← override point
```

#### createFrame Sequence

```
UIFrameBase.createFrame<FRAME>(prefabName, parent)
├── 1. if (!isInitialized) throw         ← _InitFromCanvas 전 호출 금지
├── 2. BundlePool.Spawn<FRAME>(prefabName, parent: parent ?? transform)
├── 3. instance.GetComponent<UIFrameBase>()
└── 4. frameBase?._InitFromCanvas(ownerBase)
```

### Type Constraints

| Generic | Constraint |
|---------|------------|
| `UICanvas<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `UIFrame<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `createFrame<FRAME>` | `where FRAME : Component, IPoolable<FRAME>` |

### Dependencies

| Dependency | Location |
|------------|----------|
| `CompoSingleton<T>` | `Runtime/Unity/Singletons/CompoSingleton.cs` |
| `BundlePool` | `Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable<T>` | `Runtime/Unity/Pool/IPoolable.cs` |

---

## DoD (Definition of Done) Checklist

### Files Exist
- [ ] `Runtime/Unity/UI/UICanvas.cs`
- [ ] `Runtime/Unity/UI/UIFrame.cs`

### Files Removed
- [ ] `IUiCanvasOwner.cs` 삭제 및 참조 0

### Namespace Compliance
- [ ] 모든 타입이 `namespace Devian { }` 내에 선언됨
- [ ] `Devian.UI` 등 하위 네임스페이스 없음

### Lifecycle Compliance
- [ ] `UICanvas.Awake()`가 `override` + `base.Awake()` 호출
- [ ] `UIFrame.Awake()`가 `virtual`이 아님
- [ ] `UICanvas.Awake()` 순서: base.Awake → canvas 캐시 → onAwake → initChildFrames
- [ ] `UICanvas`가 child frames를 `_InitFromCanvas(this)`로 초기화
- [ ] `UIFrame<TCanvas>`가 `onInit(TCanvas owner)`를 확장 포인트로 제공
- [ ] `UIFrame<TCanvas>`가 `owner`를 저장함

### Naming Compliance
- [ ] internal 메서드가 `_` 접두어로 시작 (`_InitFromCanvas`)
- [ ] protected 메서드가 lowerCamelCase (`onInitFromCanvas`, `createFrame`)

### createFrame Compliance
- [ ] `BundlePool.Spawn<T>()` 사용
- [ ] 생성 직후 `_InitFromCanvas(ownerBase)` 호출
- [ ] `isInitialized == false` 시 예외 발생

### Build
- [ ] 컴파일 오류 0개

---

## Reference

- **Singleton**: [30-unity-components/31-singleton/SKILL.md](../../30-unity-components/31-singleton/SKILL.md)
- **Pool Factories**: [30-unity-components/04-pool-factories/SKILL.md](../../30-unity-components/04-pool-factories/SKILL.md)
- **UIManager**: [10-ui-manager/skill.md](../10-ui-manager/skill.md)
