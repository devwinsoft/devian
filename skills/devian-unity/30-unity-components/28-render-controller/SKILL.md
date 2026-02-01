# MaterialEffectController

## 목적
Actor에 붙는 렌더 제어(Shader/Material 효과만) 컴포넌트

## 파일 위치 (SSOT)
- Runtime: `com.devian.foundation/Runtime/Unity/Render/`

## 클래스 선언

```csharp
MaterialEffectController : BaseController<GameObject>
```

- **OWNER 타입/바인딩 방식은 현재 단계에서 고정하지 않는다**
- 구현은 임시로 `GameObject` 바인딩으로 처리

## 핵심 구성요소

### IMaterialEffectDriver
- Material/Shader 관련 API만 제공
- **애니메이션 관련 API 금지**
- 필수 메서드:
  - `CaptureBaseline()` / `RestoreBaseline()`
  - `SetSharedMaterial()` / `SetSharedMaterials()`
  - `SetPropertyBlock()` / `ClearPropertyBlock()`
  - `SetVisible()`

### IMaterialEffect
- 런타임 인스턴스 인터페이스
- `Priority`: 우선순위 값
- `Apply(IMaterialEffectDriver driver)`: 효과 적용
- `Reset()`: 풀링 반환 시 초기화

### MaterialEffectAsset (ScriptableObject)
- 프로토타입 역할
- 내부 풀 관리: `Stack<IMaterialEffect>`
- `Rent()`: 인스턴스 획득. **내부 인스턴스 생성 실패(null) 시 즉시 예외(throw)로 실패 ("적당히 처리" 금지)**
- `Return(IMaterialEffect)`: 인스턴스 반환
- `CreateInstanceInternal()`: 추상 팩토리

### MaterialEffectController
- Actor에 부착
- `defaultEffectAsset`: 필수 - effect 0개일 때 적용. **런타임 강제: Awake에서 null이면 Error 로그 출력**
- `driverComponent`: 선택 - 미지정 시 같은 GO에서 IMaterialEffectDriver 자동 탐색
- `searchDriverInChildren`: 선택(기본 false) - true면 같은 GO에서 driver를 못 찾을 때 `GetComponentInChildren<IMaterialEffectDriver>(includeInactive: true)`로 한 번 더 탐색

## 우선순위 규칙 (하드)

1. **priority 큰 것이 우선**
2. **priority가 같으면 나중에 추가된 effect가 승리**
3. **effect가 0개일 때 항상 default가 적용**

## 적용 순서 (하드)

effect 변경/갱신 시마다 반드시:
```
driver.RestoreBaseline()
selectedEffect.Apply(driver)
```

## Awake 순서

1. `Init(gameObject)` 호출 (BaseController 바인딩)
2. driver resolve (driverComponent 우선, 없으면 자동 탐색, `searchDriverInChildren` true면 children 탐색)
3. `driver.CaptureBaseline()`
4. effect 0개 상태이므로 default 즉시 적용
5. **default는 Awake에서 즉시 적용되어야 하므로, 내부 `_currentAppliedHandle` 초기값을 0이 아니라 '미적용 상태(-1)'로 둔다**

## Pooling 규칙

- ScriptableObject(Asset)는 프로토타입
- 런타임 인스턴스(IMaterialEffect)를 풀링
- add 시: `asset.Rent()`
- remove 시: `asset.Return(instance)`

## 내부 엔트리 구조

```
handle(int) → entry:
  - MaterialEffectAsset asset
  - IMaterialEffect instance
  - int priority (instance.Priority)
  - long sequence
```

## 외부 API

| 메서드 | 설명 |
|--------|------|
| `int _AddEffect(MaterialEffectAsset asset)` | effect 추가, handle 반환 |
| `bool _RemoveEffect(int handle)` | effect 제거 |
| `void _ClearEffects()` | 모든 effect 제거 |
| `int _AddEffect(MATERIAL_EFFECT_ID id)` | ID로 effect 추가 |
| `void _SetDefault(MaterialEffectAsset asset)` | 런타임에서 default 교체 후 즉시 적용 |
| `int _GetCurrentAppliedHandle()` | 현재 적용 중인 effect handle (0=default, -1=none) |
| `string _GetCurrentAppliedEffectName()` | 현재 적용 중인 effect 이름 ("default", "none", 또는 asset 이름)  |

### 런타임 default 교체 규칙

- `_SetDefault(asset)` 호출 시:
  1. 기존 default instance가 있으면 `Return()` 후 제거
  2. 새 asset으로 교체 및 `Rent()`
  3. 즉시 `_ApplySelected()` 호출 (효과 스택이 비어있어도 반영)
- `_SetDefault(null)` 호출 시 Error 로그, 이후 baseline만 유지
- **default는 반드시 초기화 시 설정**하되, 런타임 교체 API 사용 가능

## 금지 사항

- IMaterialEffectDriver에 animation 관련 API 추가 금지
- 런타임 ID resolve에서 AssetDatabase/Resources.Load 직접 호출 금지 (AssetManager 캐시만)

---

## MaterialEffectManager

MaterialEffectAsset을 AssetManager 캐시에 적재하는 **CompoSingleton 기반** 매니저.

### 파일 위치

- `com.devian.foundation/Runtime/Unity/Render/MaterialEffectManager.cs`

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
- BootstrapRoot.prefab에 포함하여 사용 가능
- key가 비어있으면 경고 로그 후 스킵

### 사용 예시

```csharp
// BootstrapRoot.prefab에 MaterialEffectManager 컴포넌트 추가
// Inspector에서 _addressablesKey 설정 (예: "material-effects")
// Start()에서 자동으로 MaterialEffectAsset 로딩
```
