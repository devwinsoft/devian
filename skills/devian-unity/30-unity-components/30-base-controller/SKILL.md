# BaseController

## 목적
컨트롤러 공통 베이스 제공 (MaterialEffectController 등에서 공통 패턴 흡수)

## 현재 단계 범위
- **Owner 바인딩**만 제공
- 상세 설계(라이프사이클 훅, 상태/디스포즈, tick, 의존성 주입 등)는 **추후 별도 작업에서 확정**

## 파일 위치 (SSOT)
- Runtime: `com.devian.foundation/Runtime/Unity/Controllers/BaseController.cs`

## 하드 규약

### Namespace
```
namespace Devian
```

### 제네릭 형태
```csharp
public abstract class BaseController<TOwner> : MonoBehaviour
```

### Public API (최소)

| API | 설명 |
|-----|------|
| `TOwner Owner { get; }` | 바인딩된 Owner 참조 |
| `bool IsInitialized { get; }` | Init 호출 여부 |
| `void Init(TOwner owner)` | Owner 1회 바인딩 + OnInit 호출 |

### Protected Virtual Hook

| Hook | 설명 |
|------|------|
| `virtual void OnInit()` | Init 완료 후 확장 훅 |

## 구현 규칙

1. `Init(owner)`는 **1회만** 호출 가능 (이미 초기화됐으면 경고 로그 후 무시)
2. `Init` 내부에서 Owner 바인딩 후 `OnInit()` 호출
3. 그 외 기능(Dispose/Enable/Disable/Tick 등) 추가 금지

## 금지 사항

- Owner 바인딩 + OnInit 외 기능 추가 금지 (상세 설계는 다음 단계)
- Awake/Start에서 자동 Init 금지 (명시적 Init 호출 필요)
