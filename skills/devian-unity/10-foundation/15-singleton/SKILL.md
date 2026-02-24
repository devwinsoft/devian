# 15-singleton

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 0. 목표

- 개발자가 실수 없이 싱글톤을 사용하도록 **기본 AutoSingleton**을 제공한다.
- 필요 시 **CompoSingleton**으로 배치 책임(component)을 명시한다.
- 필요 시 **`Singleton.CreateFromResources`**로 Resources 프리팹 기반 싱글톤을 생성한다.
- 모든 싱글톤은 **단일 저장소(SingletonRegistry)**를 통해 통합 관리한다.

---

## 1. 제공 타입 (4종)


### 1-param (Registry key = 자기 자신)

#### AutoSingleton\<T\> (기본)

`T.Instance` 접근 시:
1. Registry 조회
2. 씬/자식에서 기존 컴포넌트 탐색(비활성 포함)
3. 없으면 새 GameObject 생성 + AddComponent
4. Registry 등록 + DontDestroyOnLoad 적용

**"없으면 자동 생성"이 기본 동작이다.**

**Shutdown 억제**: 에디터 종료/플레이 종료/앱 종료 중(`IsShuttingDown == true`)에는 자동 생성이 억제되며 `Instance`는 `null`을 반환한다. Shutdown 방어가 필요하면:
- `AutoSingleton<T>.IsShuttingDown`으로 사전 체크
- `Singleton.TryGet<T>(out var t)` 또는 `T.TryGet(out var t)`로 안전 조회 (자동 생성 없음)

#### CompoSingleton\<T\> (선택)

- 씬/프리팹에 컴포넌트로 붙여서 사용한다.
- `Awake()`에서 Registry에 등록한다.
- **우선순위 최고**: CompoSingleton이 등록되면 같은 타입의 Auto/Boot 인스턴스를 대체한다(Adopt).


### 2-param (Registry key = Base 타입)

2-param은 **상속 기반이 아니라 정적 helper**로 제공한다.
즉, `TSelf`는 **오직 `TBase : MonoBehaviour`만 상속**하면 된다. (다중 상속 문제 없음)

#### AutoSingleton\<TBase, TSelf\>

`AutoSingleton<T>`와 동일하되, **Registry key가 `TBase`**다.

- `TBase`: Registry key. 반드시 `MonoBehaviour` 기반 Base 타입(보통 abstract class).
- `TSelf`: 실제 MonoBehaviour 타입. `TSelf : TBase`
- `Instance`는 **`TSelf`** 타입을 반환한다 (캐스팅 최소화).
- 시스템 레이어에서는 `Singleton.Get<TBase>()`로 `TBase` 타입 접근.

제네릭 제약:
```
where TBase : MonoBehaviour
where TSelf : TBase
```

#### CompoSingleton\<TBase, TSelf\>

`CompoSingleton<T>`와 동일하되, **Registry key가 `TBase`**다.

- `TBase`: Registry key. 반드시 `MonoBehaviour` 기반 Base 타입(보통 abstract class).
- `TSelf`: 실제 MonoBehaviour 타입. `TSelf : TBase`
- `Register(this)`를 `Awake()`에서 호출해 등록한다.
- `Instance`는 **`TSelf`** 타입을 반환한다 (캐스팅 최소화).
- 시스템 레이어에서는 `Singleton.Get<TBase>()`로 `TBase` 타입 접근.

제네릭 제약:
```
where TBase : MonoBehaviour
where TSelf : TBase
```

### 1-param vs 2-param 사용 기준

| | 1-param (`<T>`) | 2-param (`<TBase, TSelf>`) |
|---|---|---|
| 형태 | 상속 기반 (`class X : AutoSingleton<X>`) | **정적 helper** (`static class AutoSingleton<TBase, TSelf>`) |
| TSelf 상속 | `MonoBehaviour` (via Singleton 상속) | **`TBase`만 상속** (다중 상속 없음) |
| Registry key | `T` (= 자기 자신) | `TBase` (= 추상 Base) |
| Instance 반환 타입 | `T` | **`TSelf`** (구체 타입) |
| TBase 접근 | `Singleton.Get<T>()` | `Singleton.Get<TBase>()` |
| 용도 | Base 분리 불필요한 경우 | 시스템 레이어(Base) / 컨텐츠 레이어(파생) 분리 |

### Auto vs Compo vs CreateFromResources 선택 기준

| | Auto | Compo | CreateFromResources |
|---|---|---|---|
| 생성 방식 | new GameObject + AddComponent | 씬/프리팹에 직접 배치 | **Resources.Load\<GameObject\> + Instantiate** |
| 우선순위 | Auto(0) 최저 | Compo(2) 최고 | **Boot(1) 중간** |
| SerializeField | 불가 (빈 GO 생성) | 가능 (씬/프리팹에 설정) | **가능 (프리팹에 미리 설정)** |
| Instance 자동 생성 | O (Instance getter) | X (씬 배치 필요) | **X (명시적 호출 필요)** |
| Base 상속 | 1-param: 불가 / 2-param: 가능 | 1-param: 불가 / 2-param: 가능 | **가능 (제약 없음)** |
| 용도 | 설정 불필요한 싱글톤 | 씬 배치가 필요한 싱글톤 | **프리팹 설정값이 필요한 싱글톤** |

---

## 2. 우선순위 규칙 (Hard Rule)

같은 타입 T에 대해 **Compo > Boot > Auto**가 항상 승리한다.

- CompoSingleton이 늦게 로드되어도, 기존 AutoSingleton/Boot 인스턴스를 **대체(Adopt)**해야 한다.
- 동일 우선순위끼리 중복은 **즉시 실패(예외)**로 처리한다.

---

## 3. Adopt 정책 (Hard Rule)

Registry에 Auto/Boot가 등록된 상태에서 Compo가 등록되면:

1. CompoSingleton을 "정본"으로 등록
2. 기존 AutoSingleton/Boot 인스턴스는 **제거(파괴)**하여 중복을 해소
3. 이 상황은 실수 가능성이 높으므로 **Error 로그**를 남긴다 (단, 앱이 계속 진행 가능해야 함)

---

## 4. 접근 API (권장)

| API | 동작 |
|-----|------|
| `Singleton.Get<T>()` | 없으면 예외 (Fail-fast) |
| `Singleton.TryGet<T>(out T)` | 없으면 false |
| `Singleton.CreateFromResources<T>(path)` | Resources에서 프리팹 로드 + Registry 등록(Boot). key=T |
| `Singleton.CreateFromResources<TBase,TSelf>(path)` | Resources에서 프리팹 로드 + Registry 등록(Boot). key=TBase |
| `T.Instance` | AutoSingleton/CompoSingleton이 제공하는 편의. Shutdown 중 null 반환 |
| `AutoSingleton<T>.IsShuttingDown` | Shutdown 구간 여부 (`OnApplicationQuit` 또는 `!Application.isPlaying`) |

---

## 5. 금지 (Hard Rule)

- 기존 싱글톤 시스템(SceneSingleton/MonoSingleton 등)을 새로 사용/추가하지 않는다.
- "조용히 Destroy로 중복을 숨기는 정책"을 기본값으로 두지 않는다.
- Registry를 우회하는 static instance 보관을 금지한다 (모든 인스턴스는 Registry가 SSOT).

---

## 6. 파일 위치 (SSOT)

```
com.devian.foundation/Runtime/Unity/Singletons/
├── SingletonSource.cs      # enum
├── SingletonRegistry.cs    # SSOT 저장소
├── Singleton.cs            # 정적 파사드
├── AutoSingleton.cs        # 1-param: AutoSingleton<T>
├── AutoSingleton2.cs       # 2-param: AutoSingleton<TBase, TSelf>
├── CompoSingleton.cs       # 1-param: CompoSingleton<T>
└── CompoSingleton2.cs      # 2-param: CompoSingleton<TBase, TSelf>
```

---

## 7. SingletonSource enum

```csharp
public enum SingletonSource
{
    Auto = 0,   // 자동 생성 (최저 우선순위)
    Boot = 1,   // Resources 로드 등록 (중간 우선순위)
    Compo = 2,  // 씬/프리팹 컴포넌트 (최고 우선순위)
}
```

---

## 8. SingletonRegistry 규약

### 저장소 구조

```csharp
private static readonly Dictionary<Type, Entry> _entries;

private readonly struct Entry
{
    public readonly object Instance;
    public readonly SingletonSource Source;
    public readonly string DebugSource;
}
```

### 등록 규칙 (Hard Rule)

| 기존 | 신규 | 결과 |
|------|------|------|
| Auto | Boot | Adopt (기존 파괴 + Warning 로그) |
| Auto | Compo | Adopt (기존 파괴 + Error 로그) |
| Boot | Compo | Adopt (기존 파괴 + Error 로그) |
| Compo | Compo | **즉시 예외** (중복 금지) |
| Boot | Boot | **즉시 예외** (중복 금지) |
| Auto | Auto | **즉시 예외** (중복 금지) |
| Compo | Boot/Auto | 무시 (Compo 우선) |
| Boot | Auto | 무시 (Boot 우선) |

### 파괴 정책

- old 인스턴스가 `Component`면 `Object.Destroy(comp)`로 **컴포넌트만 제거** (GameObject 전체 파괴 금지)
- old 인스턴스가 `UnityEngine.Object`이지만 Component가 아니면 `Object.Destroy(old)`로 제거
- 순수 C# 객체면 참조만 교체 (로그 남김)

---

## 9. CreateFromResources 규칙

**경로**: 호출자가 `string resourcePath`로 직접 지정한다.

**로드 방식**:
- `Resources.Load<GameObject>(resourcePath)` → `GetComponent<T>()` 또는 `GetComponent<TSelf>()`
- GameObject로 로드하는 이유: IL2CPP에서 리플렉션 없이 안전하게 동작
- 프리팹 루트에 싱글톤 컴포넌트가 반드시 붙어있어야 함

**등록 소스**: `SingletonSource.Boot` (중간 우선순위)

---

## 10. 사용 예시

### 기본 사용 (AutoSingleton — 1-param)

```csharp
public class GameManager : AutoSingleton<GameManager>
{
    public void StartGame() { ... }
}

// 어디서든 접근 - 없으면 자동 생성
GameManager.Instance.StartGame();
```

### 씬 배치가 필요한 경우 (CompoSingleton — 1-param)

```csharp
public class UIRoot : CompoSingleton<UIRoot>
{
    [SerializeField] private Canvas _mainCanvas;
}

// 씬에 배치된 인스턴스가 정본이 됨
// 만약 AutoSingleton/Boot으로 먼저 접근했어도 CompoSingleton이 대체함
```

### 1-param vs 2-param 선택 기준 (중요)

```
1-param: class Foo : AutoSingleton<Foo>
  → Foo의 base class = AutoSingleton<Foo> (= MonoBehaviour)
  → 다른 base class 상속 불가

2-param: class Foo : ActorObject { ... }
  → Foo의 base class = ActorObject
  → Auto/Compo는 static helper 사용
  → Resources 기반은 Singleton.CreateFromResources<ActorObject, Foo>("path") 사용
```

**Base 클래스를 상속해야 하면 2-param 또는 CreateFromResources를 사용한다.**
이는 C# 단일 상속 제약이다.

### Base 상속 + Resources 기반 싱글톤 (Singleton.CreateFromResources)

```csharp
// ── 시스템 레이어 (framework) ──
public abstract class BaseAudioSystem : MonoBehaviour
{
    public abstract void PlayBGM(string trackName);
}

// ── 컨텐츠 레이어 (game) ──
// 프리팹 배치: Assets/Resources/Singletons/GameAudioSystem.prefab
public sealed class GameAudioSystem : BaseAudioSystem
{
    [SerializeField] private AudioSource _bgmSource;

    public override void PlayBGM(string trackName) { ... }
}

// 초기화 (Bootstrap 등에서 1회 호출)
Singleton.CreateFromResources<BaseAudioSystem, GameAudioSystem>("Singletons/GameAudioSystem");

// 접근 (Registry key = BaseAudioSystem)
Singleton.Get<BaseAudioSystem>()      // OK (반환 타입: BaseAudioSystem)
```

### 시스템/컨텐츠 분리 — 2-param (CompoSingleton\<TBase, TSelf\>)

```csharp
// ── 시스템 레이어 (framework) ──
public abstract class BaseNetworkSystem : MonoBehaviour
{
    protected abstract void OnConnectionEstablished();
}

// ── 컨텐츠 레이어 (game) ──
public sealed class GameNetworkSystem : BaseNetworkSystem
{
    public static GameNetworkSystem Instance => CompoSingleton<BaseNetworkSystem, GameNetworkSystem>.Instance;

    private void Awake()
    {
        CompoSingleton<BaseNetworkSystem, GameNetworkSystem>.Register(this);
    }

    protected override void OnConnectionEstablished()
    {
        // Game-specific connection handling
    }
}

// 접근 (Registry key = BaseNetworkSystem)
Singleton.Get<BaseNetworkSystem>()      // OK (반환 타입: BaseNetworkSystem — 시스템 레이어용)
GameNetworkSystem.Instance              // OK (반환 타입: GameNetworkSystem — 컨텐츠 레이어용)
```

### 시스템/컨텐츠 분리 — 2-param (AutoSingleton\<TBase, TSelf\>)

```csharp
// ── 시스템 레이어 (framework) ──
public abstract class BaseUISystem : MonoBehaviour
{
    public abstract void ShowPopup(string message);
}

// ── 컨텐츠 레이어 (game) ──
public sealed class GameUISystem : BaseUISystem
{
    public static GameUISystem Instance => AutoSingleton<BaseUISystem, GameUISystem>.Instance;

    public override void ShowPopup(string message)
    {
        // Game-specific popup implementation
    }
}

// 접근 (Registry key = BaseUISystem)
Singleton.Get<BaseUISystem>()          // OK (반환 타입: BaseUISystem — 시스템 레이어용)
GameUISystem.Instance                  // OK (반환 타입: GameUISystem — 컨텐츠 레이어용)
```

---

## 11. Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md`
- Index: `skills/devian-unity/10-foundation/SKILL.md`
