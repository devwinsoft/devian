# Bootstrap

## 0. 목적

부트 컨테이너 프리팹과 BaseBootstrap 추상 클래스를 정의한다.

**프레임워크는 SceneTransManager 파이프라인을 통해 BootProc를 자동 트리거한다.**

---

## 1. 구성

- **부트 컨테이너 프리팹**: `Assets/Resources/Devian/Bootstrap.prefab`
- **BaseBootstrap** (abstract MonoBehaviour): 개발자가 상속하여 부트 로직을 구현하는 베이스 클래스

프리팹 생성 규칙은 [11-mobile-bootstrap](../../50-mobile-system/11-mobile-bootstrap/SKILL.md)에서 다룬다.

---

## 2. Files (SSOT)

- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/BaseBootstrap.cs`

---

## 3. 경로 (SSOT)

| 에셋 | 프로젝트 경로 | Resources.Load 경로 |
|------|---------------|---------------------|
| 부트 컨테이너 Prefab | `Assets/Resources/Devian/Bootstrap.prefab` | `Devian/Bootstrap` |

---

## 4. BaseBootstrap 클래스

### 정적 상태

```csharp
private static BaseBootstrap _instance;
private static bool _booted;

public static BaseBootstrap Instance => _instance;
```

`_instance`는 `Awake()`에서 자동 등록된다. Bootstrap 프리팹의 생성(Instantiate + DontDestroyOnLoad)은 contents 레이어가 담당한다.

### 추상 메서드

```csharp
protected abstract Task OnBootProc();
```

개발자가 구현해야 하는 부팅 로직. BootProc()에서 1회만 호출된다.

### BootProc (인스턴스 메서드)

```csharp
public async Task BootProc()
```

동작:
1. `_booted == true`면 즉시 return
2. `try { await OnBootProc(); } finally { _booted = true; }`

### Domain Reload 대응

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
{
    _instance = null;
    _booted = false;
    IsShuttingDown = false;
}
```

---

## 5. SceneTransManager와의 통합

SceneTransManager는 OnEnter 전에 BootProc 호출을 보장한다:

```csharp
// SceneTransManager.Start() 또는 LoadSceneAsync()에서
var boot = BaseBootstrap.Instance;
if (boot != null) await boot.BootProc();  // 이미 부팅이면 즉시 종료
await scene.Enter();
```

Bootstrap 통합이 필요한 씬 클래스(Awake에서 CreateFromResources, Start에서 BootProc 대기)는 프레임워크가 제공하지 않으며, contents 레이어에서 SceneBase를 상속하여 직접 구현한다.

---

## 6. 부트 컨테이너 구조

부트 컨테이너는 **BaseBootstrap 파생 컴포넌트를 정확히 1개 포함하는 프리팹**이다:

- 개발자는 `BaseBootstrap`을 상속한 클래스를 만들어 프리팹에 부착
- `OnBootProc()`에서 초기화 로직 구현
- 추가로 필요한 Manager 컴포넌트들을 함께 부착 가능

**프레임워크가 BaseBootstrap 파생 컴포넌트를 자동 추가하지 않는다.** 개발자가 직접 추가해야 한다.

### 필수 컴포넌트 부착

BaseBootstrap.Awake()에서 `ensureRequiredComponents()`가 호출된다.

- BaseBootstrap 기본 구현은 필수 매니저를 추가하지 않는다.
- `CompoSingleton` 계열은 런타임 `AddComponent`로 만들지 않고, bootstrap prefab/scene object에 미리 부착해야 한다.
- 소비자 모듈/도메인은 파생 bootstrap에서 필요한 컴포넌트가 이미 부착되었는지 검증/보장한다.

---

## 7. 테스트 규약

PlayMode 테스트는 테스트 씬에 부트 컨테이너를 배치하거나, SetUp에서 직접 instantiate 한다.

---

## 8. Reference

- Parent: `skills/devian-unity/10-foundation/SKILL.md`
- SceneTransManager: `skills/devian-unity/10-foundation/17-scene-trans-manager/SKILL.md`
- Singleton: `skills/devian-unity/10-foundation/15-singleton/SKILL.md`
