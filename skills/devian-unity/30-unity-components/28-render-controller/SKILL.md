# MaterialEffectController (v2)

## 목적

단일 Renderer의 Material[] 배열(sharedMaterials) 스위치 전용 컴포넌트.

## 핵심 정책 (v2)

- **1:1 매칭**: Controller ↔ Driver(Renderer) 1개
- **default = baseline**: effect 0개일 때는 baseline(초기 Material 상태)이 적용됨
- **slot mismatch 허용**: 검증/차단 없이 그대로 설정
- **PropertyBlock/Common 제거**: Material[] 스위치만 지원

## 파일 위치 (SSOT)

- Runtime: `com.devian.foundation/Runtime/Unity/MaterialEffect/`

## 클래스 선언

```csharp
MaterialEffectController : BaseController<GameObject>
```

## 핵심 구성요소

### IMaterialEffectDriver (v2)

단일 Renderer를 위한 Material 교체 인터페이스.

| 메서드 | 설명 |
|--------|------|
| `bool IsValid { get; }` | Driver 유효성 (Renderer 존재 여부) |
| `void CaptureBaseline()` | 현재 sharedMaterials를 clone하여 baseline 저장 |
| `void RestoreBaseline()` | baseline으로 복원 + apply-clone 정리 |
| `void DisposeBaseline()` | baseline clone들 Destroy (OnDestroy용) |
| `void SetSharedMaterials(Material[] materials)` | Material[] 교체 (slot mismatch 허용) |
| `void SetVisible(bool visible)` | Renderer.enabled 설정 |

**삭제된 API (v1 → v2):**
- `RendererCount`, `GetMaterialSlotCount`
- `SetSharedMaterial(int rendererIndex, ...)`, `SetSharedMaterials(int rendererIndex, ...)`
- `SetPropertyBlock`, `ClearPropertyBlock`, `SetFloat/Int/Color`, `ApplyPropertyBlocks`
- `ApplyCommon`

### IMaterialEffect

런타임 인스턴스 인터페이스.

| 멤버 | 설명 |
|------|------|
| `int Priority { get; }` | 우선순위 값 |
| `void Apply(IMaterialEffectDriver driver)` | 효과 적용 |
| `void Reset()` | 풀링 반환 시 초기화 |

### MaterialEffectAsset (ScriptableObject)

- 프로토타입 역할
- 내부 풀 관리: `Stack<IMaterialEffect>`
- `Rent()`: 인스턴스 획득. **내부 인스턴스 생성 실패(null) 시 즉시 예외(throw)**
- `Return(IMaterialEffect)`: 인스턴스 반환
- `CreateInstanceInternal()`: 추상 팩토리

### MaterialEffectController

| 필드 | 타입 | 설명 |
|------|------|------|
| `_driverComponent` | Component | Driver 지정 (선택) |
| `_searchDriverInChildren` | bool | children 탐색 (기본 false, v2 정책상 비권장) |

## 우선순위 규칙 (하드)

1. **priority 큰 것이 우선**
2. **priority가 같으면 나중에 추가된 effect가 승리**
3. **effect가 0개일 때 baseline이 적용됨** (handle 0)

## 적용 순서 (하드)

effect 변경/갱신 시마다:
```
driver.RestoreBaseline()
if (selectedEffect != null) selectedEffect.Apply(driver)
```

## Awake 순서

1. `Init(gameObject)` 호출 (BaseController 바인딩)
2. driver resolve (driverComponent 우선, 없으면 자동 탐색)
3. `driver.CaptureBaseline()`
4. effect 0개 상태이므로 baseline(handle 0) 적용

## 외부 API

| 메서드 | 설명 |
|--------|------|
| `int _AddEffect(MaterialEffectAsset asset)` | effect 추가, handle 반환 |
| `bool _RemoveEffect(int handle)` | effect 제거 |
| `void _ClearEffects()` | 모든 effect 제거 |
| `int _AddEffect(MATERIAL_EFFECT_ID id)` | ID로 effect 추가 |
| `int _GetCurrentAppliedHandle()` | 현재 적용 중인 effect handle (0=baseline, -1=none) |
| `string _GetCurrentAppliedEffectName()` | 현재 적용 중인 effect 이름 |

## Effect Asset 종류 (v2)

### MaterialSetMaterialEffectAsset (권장)

Material[] 배열을 그대로 적용.

```csharp
[CreateAssetMenu(menuName = "Devian/Material Effects/Material Set")]
public sealed class MaterialSetMaterialEffectAsset : MaterialEffectAsset
{
    [SerializeField] private Material[] _materials;
}
```

### NoOpMaterialEffectAsset

아무 효과도 적용하지 않음 (baseline 상태 유지).

## MaterialEffectDriver (v2)

단일 Renderer 전용 구현체.

| 필드 | 타입 | 설명 |
|------|------|------|
| `_renderer` | Renderer | 대상 Renderer (null이면 같은 GO에서 자동 획득) |
| `_cloneOnApply` | bool | clone-on-apply 활성화 (기본 true) |

### clone-on-apply 정책

- 에셋 머티리얼을 그대로 꽂지 않고 clone해서 적용
- 이미 Devian clone이면 재-clone 금지
- effect 전환(RestoreBaseline) 시 apply-clone 전부 Dispose

## 금지 사항

- IMaterialEffectDriver에 animation 관련 API 추가 금지
- 런타임 ID resolve에서 AssetDatabase/Resources.Load 직접 호출 금지 (AssetManager 캐시만)
- slot mismatch 검증/차단/자동 보정 금지

---

## Edit Mode Preview/Save (Editor 전용)

### 핵심 정책

- **Edit Mode 전용**: Play Mode에서 Preview/Save 기능 금지
- **Play Bake 금지**: Play 중 Snapshot Save 불가
- **baseline 오염 방지**: Play 진입 시 자동으로 모든 Preview OFF

### Editor API (UNITY_EDITOR only)

| 메서드 | 설명 |
|--------|------|
| `bool EditorPreviewIsActive` | Preview 활성화 여부 |
| `void EditorPreviewOn(MaterialEffectAsset effect)` | Preview 켜기 (Edit Mode only) |
| `void EditorPreviewOff()` | Preview 끄고 baseline 복원 |
| `void EditorSaveSnapshotTo(MaterialSetMaterialEffectAsset target)` | Renderer의 현재 materials를 asset에 저장 |

### Preview 동작 규칙

1. **Edit Mode에서만 동작** - `Application.isPlaying == true`면 무시
2. **baseline 저장** - Preview ON 시 현재 sharedMaterials를 NonSerialized 필드에 저장
3. **직접 교체** - Driver를 거치지 않고 `Renderer.sharedMaterials` 직접 설정
4. **PropertyBlock clear** - Preview ON/OFF 시 `renderer.SetPropertyBlock(null)` 호출

### Snapshot Save 검증 규칙 (Hard)

저장 전 모든 material slot에 대해 다음 검증 통과 필수:

| 검증 | 실패 시 |
|------|---------|
| `material != null` | 저장 거부 |
| `AssetDatabase.Contains(material) == true` | 저장 거부 (runtime instance 금지) |
| `hideFlags`에 DontSave 계열 없음 | 저장 거부 |
| `name`이 `__DevianClone__`으로 시작하지 않음 | 저장 거부 (clone 저장 금지) |

### Play Mode 진입 시 자동 클린업

`MaterialEffectEditorHooks`가 `EditorApplication.playModeStateChanged` 구독:
- `ExitingEditMode` 시 모든 `MaterialEffectController`의 `_EditorPreviewOff()` 호출
- baseline 오염 방지 (Preview 상태가 Play 시작 baseline으로 캡처되는 사고 방지)

### Custom Inspector (MaterialEffectControllerEditor)

파일: `Editor/MaterialEffectControllerEditor.cs`

UI 구성:
- **Preview 섹션**: Effect 선택 + Preview On/Off 버튼 + 상태 표시
- **Snapshot 섹션**: Target Asset 선택 + Save 버튼

Undo/Dirty 처리:
- Preview 전 `Undo.RecordObject(renderer, ...)` 호출
- 변경 후 `EditorUtility.SetDirty(renderer)` 호출

---

## MaterialEffectManager

MaterialEffectAsset을 AssetManager 캐시에 적재하는 **CompoSingleton 기반** 매니저.

### 파일 위치

- `com.devian.foundation/Runtime/Unity/MaterialEffect/MaterialEffectManager.cs`

### 선언

```csharp
public sealed class MaterialEffectManager : CompoSingleton<MaterialEffectManager>
```

### 인스펙터 필드

| 필드 | 타입 | 설명 |
|------|------|------|
| `_addressablesKey` | `string` | MaterialEffectAsset 번들의 Addressables key/label |

### 동작

- `Start()`에서 `_addressablesKey`가 설정되어 있으면 `AssetManager.LoadBundleAssets<MaterialEffectAsset>(key)` 호출
- key가 비어있으면 경고 로그 후 스킵
