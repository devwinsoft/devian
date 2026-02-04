# UICanvas / UIFrame Policy

## Namespace Policy

| Rule | Description |
|------|-------------|
| **MUST** | `namespace Devian` 단일 루트 네임스페이스만 사용 |
| **FAIL** | `namespace Devian.UI` 등 하위 네임스페이스 사용 시 |

---

## Naming Policy

C# 메서드 네이밍(internal `_` 접두어, protected lowerCamelCase)은 상위 Devian 네이밍 정책을 따른다.

---

## Interface Policy

| Rule | Description |
|------|-------------|
| **MUST** | IUiCanvasOwner 인터페이스 사용 금지 (제거됨) |
| **MUST** | Canvas→Frame 연결은 타입 캐스팅으로 처리 |

---

## Lifecycle Policy

| Rule | Description |
|------|-------------|
| **MUST** | `Awake()` → `onAwake()` 패턴 사용 |
| **MUST** | UICanvas.Awake()는 `override` + `base.Awake()` 호출 (CompoSingleton 상속) |
| **MUST** | UIFrame.Awake()는 non-virtual (MonoBehaviour 직접 상속) |
| **MUST** | UICanvas는 `onAwake()` 완료 후 child frame `_InitFromCanvas(this)` 수행 |
| **MUST** | UIFrameBase._InitFromCanvas()는 owner 저장만 수행 |
| **MUST** | 실제 초기화 로직은 `onInit(TCanvas owner)`에서 처리 |
| **MUST** | `_InitFromCanvas()` 중복 호출 방지 (isInitialized 체크) |

---

## Creation Policy

| Rule | Description |
|------|-------------|
| **MUST** | `UIFrameBase.createFrame<T>()` 는 `BundlePool`로 생성 |
| **MUST** | 생성 직후 `_InitFromCanvas(ownerBase)` 호출 |
| **MUST** | `createFrame`은 `_InitFromCanvas` 이전 호출 시 예외 발생 |

---

## Prohibited Actions

| Action | Reason |
|--------|--------|
| UIFrame.`Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| UICanvas.Awake()에서 `base.Awake()` 누락 | CompoSingleton 등록 실패 |
| IUiCanvasOwner 인터페이스 사용 | 제거됨, 타입 캐스팅 방식으로 대체 |
| 자동 billboard 컴포넌트 / 자동 업데이트 | helper 메서드만 제공 |
| Canvas 설정 변경 API (`UseSharedWorldCamera` 등) | Inspector에서 설정, 코드는 검증/헬퍼만 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

---

## Validation Requirements

| Check | Expected |
|-------|----------|
| `ScreenSpaceOverlay` + `worldCamera != null` | **FAIL** |
| `ScreenSpaceCamera` + `worldCamera == null` | **FAIL** |

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
