# UICanvas / UIFrame System Overview

## Purpose

Unity UI를 위한 UICanvas/UIFrame 기본 구조를 제공한다.
Canvas owner와 UI 기능 단위(Frame)의 초기화 수명주기를 표준화한다.

---

## Terms

| Term | Definition |
|------|------------|
| **UICanvas** | Canvas owner. CompoSingleton 기반 싱글톤으로, 자식 Frame들의 초기화 주체 |
| **UIFrame** | Canvas 하위 UI 기능 단위. UICanvas로부터 _InitFromCanvas 호출을 받아 초기화됨 |
| **UIFrameBase** | UIFrame의 비제네릭 기반 클래스. _InitFromCanvas(MonoBehaviour) 진입점 제공 |
| **UIFrame\<TCanvas\>** | 타입 안전 버전. 강타입 owner 참조 + onInit(TCanvas) 확장점 제공 |

---

## Usage Flow

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

---

## Includes / Excludes

### Includes
- Init lifecycle (Awake → onAwake, _InitFromCanvas → onInitFromCanvas → onInit)
- createFrame for dynamic frame creation via BundlePool
- Validation helpers (Validate)
- Coordinate conversion helpers (TryWorldToOverlayLocal)
- Billboard helpers (ComputeBillboardRotation, ApplyBillboard)

### Excludes
- UI 라우팅 / 스택 / 네비게이션 시스템
- UI 애니메이션 시스템
- Canvas 설정 변경 API (renderMode, worldCamera는 Inspector에서 설정)
- 자동 billboard 컴포넌트 / 자동 업데이트
- ~~IUiCanvasOwner 인터페이스~~ (제거됨)

---

## Related Documents

- **Policy**: [01-policy/ui-canvas-frames.md](../01-policy/ui-canvas-frames.md)
- **SSOT**: [03-ssot/ui-canvas-frames.md](../03-ssot/ui-canvas-frames.md)
- **Singleton**: [30-unity-components/31-singleton/SKILL.md](../30-unity-components/31-singleton/SKILL.md)
- **Pool Factories**: [30-unity-components/04-pool-factories/SKILL.md](../30-unity-components/04-pool-factories/SKILL.md)
