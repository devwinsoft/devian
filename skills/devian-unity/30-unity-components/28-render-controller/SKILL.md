# RenderController

## 목적
Actor에 붙는 렌더 제어(Shader/Material 효과만) 컴포넌트

## 파일 위치 (SSOT)
- Runtime: `com.devian.foundation/Runtime/Unity/Render/`

## 클래스 선언

```csharp
RenderController : BaseController<GameObject>
```

- **OWNER 타입/바인딩 방식은 현재 단계에서 고정하지 않는다**
- 구현은 임시로 `GameObject` 바인딩으로 처리

## 핵심 구성요소

### IRenderDriver
- Material/Shader 관련 API만 제공
- **애니메이션 관련 API 금지**
- 필수 메서드:
  - `CaptureBaseline()` / `RestoreBaseline()`
  - `SetSharedMaterial()` / `SetSharedMaterials()`
  - `SetPropertyBlock()` / `ClearPropertyBlock()`
  - `SetVisible()`

### IRenderEffect
- 런타임 인스턴스 인터페이스
- `Priority`: 우선순위 값
- `Apply(IRenderDriver driver)`: 효과 적용
- `Reset()`: 풀링 반환 시 초기화

### RenderEffectAsset (ScriptableObject)
- 프로토타입 역할
- 내부 풀 관리: `Stack<IRenderEffect>`
- `Rent()`: 인스턴스 획득
- `Return(IRenderEffect)`: 인스턴스 반환
- `CreateInstanceInternal()`: 추상 팩토리

### RenderController
- Actor에 부착
- `defaultEffectAsset`: 필수 - effect 0개일 때 적용
- `driverComponent`: 선택 - 미지정 시 같은 GO에서 IRenderDriver 자동 탐색

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
2. driver resolve (driverComponent 우선, 없으면 자동 탐색)
3. `driver.CaptureBaseline()`
4. effect 0개 상태이므로 default 즉시 적용

## Pooling 규칙

- ScriptableObject(Asset)는 프로토타입
- 런타임 인스턴스(IRenderEffect)를 풀링
- add 시: `asset.Rent()`
- remove 시: `asset.Return(instance)`

## 내부 엔트리 구조

```
handle(int) → entry:
  - RenderEffectAsset asset
  - IRenderEffect instance
  - int priority (instance.Priority)
  - long sequence
```

## 외부 API

| 메서드 | 설명 |
|--------|------|
| `int _AddEffect(RenderEffectAsset asset)` | effect 추가, handle 반환 |
| `bool _RemoveEffect(int handle)` | effect 제거 |
| `void _ClearEffects()` | 모든 effect 제거 |
| `int _AddEffect(RENDER_EFFECT_ID id)` | ID로 effect 추가 |

## 금지 사항

- IRenderDriver에 animation 관련 API 추가 금지
- 런타임 ID resolve에서 AssetDatabase/Resources.Load 직접 호출 금지 (AssetManager 캐시만)
