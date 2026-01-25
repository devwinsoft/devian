# 01-singleton

Status: ACTIVE  
AppliesTo: v11  
Type: Policy

---

## 1. 목적

Unity용 Singleton **정책**을 정의한다.  
이 스킬은 **싱글톤 계열 정책 묶음**이며, 정확히 3개의 타입만 제공한다.

---

## 2. 범위

### 포함

- **MonoSingleton\<T\>** - Manual / Persistent (자동 생성 없음)
- **AutoSingleton\<T\>** - AutoCreate / Persistent (Instance 접근 시 자동 생성)
- **ResSingleton\<T\>** - Resources / Persistent (Resources.Load로 생성)
- 공통 규약 (중복 처리, 영속성, 네임스페이스, 스레드)
- API 시그니처 정본
- 확장 정책

### 제외

- Scene 종속형 Singleton (별도 스킬로 분리)
- 실제 C# 구현 코드
- 위 3개 외 다른 singleton 타입

---

## 3. 용어 정의

| 용어 | 정의 |
|------|------|
| Persistent Singleton | `DontDestroyOnLoad`로 씬 전환에도 유지되는 싱글톤 |
| Scene Singleton | 특정 씬 내에서만 유효한 싱글톤 (이 스킬에서 제외) |
| Manual Singleton | 씬 배치 또는 명시적 등록으로만 인스턴스 생성 (MonoSingleton) |
| AutoCreate Singleton | Instance 접근 시 인스턴스가 없으면 자동 생성 (AutoSingleton) |
| Resources Singleton | Resources.Load로 프리팹을 로드하여 생성 (ResSingleton) |

---

## 4. 공통 규약 (Common Rules)

모든 타입에 적용되는 기본 규칙이다. 타입별 섹션에서 더 엄격하게 정의할 수 있으나, 완화는 금지.

### 4.1 네이밍 규약 (정본)

제공 타입 이름은 **정확히 3개만** 사용:

| 타입 이름 | 용도 |
|-----------|------|
| `MonoSingleton<T>` | Manual / Persistent |
| `AutoSingleton<T>` | AutoCreate / Persistent |
| `ResSingleton<T>` | Resources / Persistent |

- **위 이름 외 "더 긴 이름/다른 접두사/접미사" 금지**
- 네임스페이스: **`namespace Devian`** 고정

### 4.2 영속성 (Persistence)

- Singleton은 기본적으로 **Persistent**이며 `DontDestroyOnLoad(gameObject)` 적용 전제
- 씬 전환 시에도 인스턴스 유지

### 4.3 중복 인스턴스 처리 (정본)

```
Awake()에서 기존 인스턴스(_instance)가 있고 자기 자신이 아니면:
  → 새로 뜬 쪽(this)을 Destroy (기존 유지)
```

- 기존 인스턴스 우선 정책
- **"둘 다 살려두기" 금지**

### 4.4 스레드 규약 (정본)

- Unity 오브젝트 생성/파괴/Resources.Load/Instantiate는 **메인 스레드에서만 허용**
- **메인 스레드가 아닐 경우: throw (정본)**
- `Awake()`, `Register()`, `Create()`, `Load()` 등은 메인 스레드에서만 동작

### 4.5 씬 종속형 제외

- 씬 종속형 singleton은 이 스킬 범위 밖
- 별도 스킬(예: `02-scene-singleton`)로 분리

### 4.6 멀티스레드 경계

> Logger 같은 시스템에서 "어떤 스레드에서든 호출"을 원하면,  
> 그것은 **singleton 템플릿이 아니라 sink/pump 설계**로 해결한다.  
> 예: `Logger.Log()`는 큐에 메시지를 넣고, 메인 스레드의 pump가 처리.

---

## 5. 타입 A: MonoSingleton\<T\> (Manual / Persistent)

### 5.1 목적

자동 생성 없이 **명시적으로 존재하는 인스턴스**만 singleton으로 관리한다.  
Instance가 없으면 예외를 발생시켜 개발자가 누락을 즉시 인지하도록 한다.

### 5.2 필수 규약 (정본)

| 항목 | 규칙 |
|------|------|
| **자동 생성** | **금지** - Instance는 생성하지 않는다 |
| **Resources.Load** | **금지** |
| **Instance 없을 때** | **throw** (`InvalidOperationException`) |
| **허용되는 생성 방식** | 씬 배치 또는 명시적 Register 호출 |
| **중복 처리** | 신규 Destroy, 기존 유지 |
| **영속성** | 등록된(유효한) 인스턴스에 DontDestroyOnLoad 적용 |

- 인스턴스는 **씬 배치** 또는 **명시적 등록 흐름**에서만 만들어짐
- 템플릿은 **등록/중복 방지/영속성 보장**만 담당

### 5.3 Instance 실패 정책 (정본)

```csharp
public static T Instance
{
    get
    {
        if (_instance == null)
            throw new InvalidOperationException(
                $"[{typeof(T).Name}] Instance not found. " +
                "Ensure the singleton is placed in scene or explicitly registered.");
        return _instance;
    }
}
```

### 5.4 API 시그니처 (정본)

```csharp
namespace Devian
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        /// <summary>인스턴스 존재 여부</summary>
        public static bool HasInstance { get; }

        /// <summary>싱글톤 인스턴스 (없으면 throw)</summary>
        /// <exception cref="InvalidOperationException">인스턴스가 존재하지 않을 때</exception>
        public static T Instance { get; }

        /// <summary>명시적 인스턴스 등록 (메인 스레드 전용, 자동 생성 아님)</summary>
        public static void Register(T instance);
    }
}
```

- `Register`가 있어도 **자동 생성은 아님**을 명확히 한다

---

## 6. 타입 B: AutoSingleton\<T\> (AutoCreate / Persistent)

### 6.1 목적

사용자는 `T.Instance` 접근 시 인스턴스가 없으면 **자동 생성**된다.  
단, Unity 제약으로 인해 **자동 생성은 메인 스레드에서만** 허용한다.

### 6.2 Instance 동작 순서 (정본)

Instance 로직은 **반드시 아래 순서**를 따른다:

```
1. _instance가 있으면 → 반환
2. 없으면 씬에서 기존 인스턴스 탐색 후 채택
   - FindObjectOfType<T>(includeInactive: true) 또는 동등한 방식
   - 활성/비활성 오브젝트 모두 탐색
3. 그래도 없으면 → 새 GameObject 생성 + AddComponent<T>()
4. 첫 인스턴스에 DontDestroyOnLoad 적용
```

| 단계 | 설명 |
|------|------|
| Step 1 | 캐시된 인스턴스 확인 |
| Step 2 | 씬 탐색 (Find-first) - 비활성 포함 |
| Step 3 | 자동 생성 (메인 스레드 강제) |
| Step 4 | 영속성 적용 |

### 6.3 필수 규약 (정본)

| 항목 | 규칙 |
|------|------|
| **메인 스레드 강제** | Step 2~3 진입 시 메인 스레드가 아니면 **throw** |
| **Resources.Load** | **금지** |
| **중복 처리** | 신규 Destroy, 기존 유지 |
| **종료 중 재생성** | **금지** (throw) |

### 6.4 스레드 규칙 (정본)

```csharp
// 정본: 메인 스레드가 아니면 예외
if (!IsMainThread())
    throw new InvalidOperationException(
        $"[{typeof(T).Name}] Cannot auto-create singleton from non-main thread.");
```

- 이미 인스턴스가 존재하면 어떤 스레드에서든 Instance 접근 가능
- **단, Unity 객체 접근이 섞이므로 권장하지 않음**

### 6.5 종료 중 재생성 금지 (정본)

```csharp
private static bool _isQuitting = false;

private void OnApplicationQuit()
{
    _isQuitting = true;
}

public static T Instance
{
    get
    {
        if (_isQuitting)
            throw new InvalidOperationException(
                $"[{typeof(T).Name}] Cannot access singleton during application quit.");
        // ... 나머지 로직
    }
}
```

- **종료 중 Instance 호출 시: throw (정본)**

### 6.6 API 시그니처 (정본)

```csharp
namespace Devian
{
    public class AutoSingleton<T> : MonoBehaviour where T : AutoSingleton<T>
    {
        /// <summary>인스턴스 존재 여부</summary>
        public static bool HasInstance { get; }

        /// <summary>
        /// 싱글톤 인스턴스 (없으면 자동 생성)
        /// - 메인 스레드가 아니면 throw
        /// - 종료 중이면 throw
        /// </summary>
        public static T Instance { get; }
    }
}
```

---

## 7. 타입 C: ResSingleton\<T\> (Resources / Persistent)

### 7.1 목적

**Resources.Load**로 프리팹을 로드해 생성하는 singleton을 제공한다.  
**이 타입에서만 Resources 사용을 허용한다.** (MonoSingleton/AutoSingleton은 금지)

### 7.2 필수 규약 (정본)

| 항목 | 규칙 |
|------|------|
| **생성 방식** | `Load(string resourcePath)` 명시적 호출 (정본) |
| **경로 자동 유추** | **금지** (리플렉션/타입명 조합 등 금지) |
| **Instance 없을 때** | **throw** (사용자가 Load를 먼저 호출해야 함) |
| **메인 스레드 강제** | Load 호출 시 메인 스레드가 아니면 **throw** |
| **프리팹에 T 없음** | **throw** (리소스/프리팹 구성 오류) |
| **중복 처리** | 이미 있으면 기존 반환 (추가 로드 무시) |

### 7.3 Load 동작 순서 (정본)

```
1. 메인 스레드 확인 → 아니면 throw
2. _instance가 이미 있으면 → 기존 인스턴스 반환 (추가 로드 무시)
3. Resources.Load<GameObject>(resourcePath)로 프리팹 로드
4. prefab == null이면 throw (리소스 없음)
5. Instantiate(prefab) 후 GetComponent<T>()
6. component == null이면 throw (프리팹에 T 없음)
7. DontDestroyOnLoad 적용
8. _instance에 할당 후 반환
```

### 7.4 Instance 정책 (정본)

```csharp
public static T Instance
{
    get
    {
        if (_instance == null)
            throw new InvalidOperationException(
                $"[{typeof(T).Name}] Instance not found. " +
                "Call Load(resourcePath) first to create the singleton.");
        return _instance;
    }
}
```

- **Instance는 존재하지 않으면 throw**
- 사용자가 `Load(path)`를 먼저 호출해야 함
- Instance가 내부적으로 마지막 경로를 기억하고 로드하는 방식은 **금지** (상태/추측 발생)

### 7.5 API 시그니처 (정본)

```csharp
namespace Devian
{
    public class ResSingleton<T> : MonoBehaviour where T : ResSingleton<T>
    {
        /// <summary>인스턴스 존재 여부</summary>
        public static bool HasInstance { get; }

        /// <summary>싱글톤 인스턴스 (없으면 throw, Load 선행 필요)</summary>
        /// <exception cref="InvalidOperationException">인스턴스가 존재하지 않을 때</exception>
        public static T Instance { get; }

        /// <summary>
        /// Resources에서 프리팹 로드 후 인스턴스 생성 (메인 스레드 전용)
        /// - 이미 인스턴스가 있으면 기존 반환
        /// - 프리팹이 없거나 T 컴포넌트가 없으면 throw
        /// </summary>
        /// <param name="resourcePath">Resources 폴더 내 프리팹 경로</param>
        public static T Load(string resourcePath);
    }
}
```

### 7.6 금지사항 (ResSingleton 내부)

| 금지 항목 | 이유 |
|-----------|------|
| resourcePath 없이 자동 경로 유추 | 리플렉션/타입명 조합 등은 예측 불가능한 동작 유발 |
| MonoSingleton/AutoSingleton에 Resources 허용 역수입 | SSOT 위반 |

---

## 8. 확장 정책 (추가 Singleton 타입)

### 8.1 확장 규칙

이 스킬(01-singleton-template)은 **싱글톤 템플릿 묶음**이며, 추가 타입은 이 문서에 섹션으로 추가한다.

| 규칙 | 설명 |
|------|------|
| 공통 규약 준수 | 섹션 4의 공통 규약을 반드시 준수 |
| 완화 금지 | 공통 규약보다 느슨한 정책은 불가 |
| 이름 규칙 | 타입 이름은 짧게, 기존 3종 이름은 변경 금지 |
| Scene Singleton | 별도 스킬로 분리 (예: `02-scene-singleton`) |

### 8.2 신규 타입 추가 시 필수 정의 항목

| 항목 | 설명 |
|------|------|
| 생성 방식 | 자동/수동/리소스, 허용 방식 |
| 중복 처리 | 공통 규약 준수 여부 |
| 영속성 | Persistent/Scene-bound |
| 스레드 규칙 | 메인 스레드 강제 여부, 예외 처리 |

---

## 9. DoD 체크리스트

### 네이밍/구조

- [x] `MonoSingleton<T>`, `AutoSingleton<T>`, `ResSingleton<T>` 이름이 SSOT로 명시
- [x] 네임스페이스 `Devian` 고정 규약이 명시
- [x] "씬 종속형은 별도 스킬"이 명시

### MonoSingleton

- [x] 자동 생성 금지가 명시
- [x] Resources.Load 금지가 명시
- [x] Instance 없으면 throw가 명시

### AutoSingleton

- [x] Find-first → Create 순서가 명시
- [x] 메인 스레드 강제 (비메인 시 throw)가 명시
- [x] Resources.Load 금지가 명시
- [x] 종료 중 재생성 방지 (throw)가 명시

### ResSingleton

- [x] Resources.Load 허용 범위가 ResSingleton만으로 제한
- [x] `Load(string resourcePath)` 정본 API가 명시
- [x] 프리팹에 T가 없으면 throw가 명시
- [x] Instance 정책 (Load 선행 필요, 없으면 throw)이 명시
- [x] 경로 자동 유추 금지가 명시

---

## 10. Generated Output (정본)

이 스킬의 C# 코드는 빌드 시 자동 생성된다.

### 생성 대상 패키지

- `com.devian.unity`

### 생성 위치 (고정)

```
com.devian.unity/Runtime/
├── _Shared/
│   └── UnityMainThread.cs     (공용 내부 헬퍼)
└── Singleton/
    ├── MonoSingleton.cs
    ├── AutoSingleton.cs
    └── ResSingleton.cs
```

### 생성 주체 (정본)

- `framework-ts/tools/builder/build.js` 의 static UPM 처리 단계
- `processStaticUpmPackage('com.devian.unity')` 에서 staging에 생성
- 생성 순서: `_Shared` → `Singleton` → `Pool`

### 생성 파일 규칙

| 파일 | 타입 | 네임스페이스 |
|------|------|-------------|
| `MonoSingleton.cs` | `MonoSingleton<T>` | `Devian` |
| `AutoSingleton.cs` | `AutoSingleton<T>` | `Devian` |
| `ResSingleton.cs` | `ResSingleton<T>` | `Devian` |

### 공용 헬퍼 (Pool과 공유)

- `_Shared/UnityMainThread.cs` - 메인 스레드 검증 헬퍼
- 호출 형태: `UnityMainThread.EnsureOrThrow(string context)`
- Singleton은 이 공용 헬퍼를 참조한다

### 중복 인스턴스 Destroy 정본

```csharp
// 정본: 중복 인스턴스 처리 시
UnityEngine.Object.Destroy(gameObject);
```

- `00-unity-object-destruction/SKILL.md` 규약을 따른다
- `delete` 키워드 사용 금지 (C#에 없음)
- 컴포넌트가 아닌 `gameObject`를 Destroy

### 주의사항

- 제공 singleton 타입은 3종만이며, 공용 헬퍼는 `_Shared/`에 위치
- **`Singleton/UnityMainThread.cs`는 생성하지 않음** (공용 `_Shared/` 사용)
- 소스 레포지토리(`framework-cs/upm/com.devian.unity`)에는 생성 폴더가 없어도 됨
- 빌드 후 `UnityExample/Packages/com.devian.unity/Runtime/Singleton/*.cs` 에 파일 존재
- `Runtime/Templates/` 레거시 경로가 존재하면 FAIL

---

## 11. Reference

- Parent: `skills/devian-upm/30-unity-components/SKILL.md`
- Related: `skills/devian-upm/20-packages/com.devian.unity/SKILL.md`
- Related: `skills/devian-upm/30-unity-components/00-unity-object-destruction/SKILL.md` (Destroy 규약)
