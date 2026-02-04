# UICanvas / UIFrame SSOT

## Canonical Code Path

```
framework-cs/upm/com.devian.foundation/Runtime/Unity/UI/
```

---

## File List

| File | Purpose |
|------|---------|
| `UICanvas.cs` | UICanvas<T> 추상 클래스 + BillboardMode enum |
| `UIFrame.cs` | UIFrameBase + UIFrame<TCanvas> 클래스 |

### Removed Files
| File | Reason |
|------|--------|
| ~~`IUiCanvasOwner.cs`~~ | 인터페이스 제거됨, 타입 캐스팅 방식으로 대체 |

---

## Public API Signatures

### BillboardMode

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

### UICanvas\<TCanvas\>

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

### UIFrameBase

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

### UIFrame\<TCanvas\>

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

---

## Initialization Sequence (Canonical)

### UICanvas Initialization

```
UICanvas.Awake()  (override from CompoSingleton)
├── 1. base.Awake()                      ← CompoSingleton 등록
├── 2. canvas = GetComponent<Canvas>()
├── 3. onAwake()                         ← override point
└── 4. initChildFrames()
    └── foreach UIFrameBase in children
        └── frame._InitFromCanvas(this)
```

### UIFrame Initialization

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

### createFrame Sequence

```
UIFrameBase.createFrame<FRAME>(prefabName, parent)
├── 1. if (!isInitialized) throw         ← _InitFromCanvas 전 호출 금지
├── 2. BundlePool.Spawn<FRAME>(prefabName, parent: parent ?? transform)
├── 3. instance.GetComponent<UIFrameBase>()
└── 4. frameBase?._InitFromCanvas(ownerBase)
```

---

## Type Constraints

| Generic | Constraint |
|---------|------------|
| `UICanvas<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `UIFrame<TCanvas>` | `where TCanvas : MonoBehaviour` |
| `createFrame<FRAME>` | `where FRAME : Component, IPoolable<FRAME>` |

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `CompoSingleton<T>` | `Runtime/Unity/Singletons/CompoSingleton.cs` |
| `BundlePool` | `Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable<T>` | `Runtime/Unity/Pool/IPoolable.cs` |
