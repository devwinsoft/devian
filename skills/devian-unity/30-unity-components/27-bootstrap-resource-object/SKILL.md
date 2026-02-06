# Bootstrap Resource Object

## 0. 목적

부트 컨테이너 프리팹과 BaseBootstrap 추상 클래스를 정의한다.

**프레임워크는 SceneTransManager 파이프라인을 통해 BootProc를 자동 트리거한다.**

---

## 1. 구성

- **부트 컨테이너 프리팹**: `Assets/Resources/Devian/Bootstrap.prefab`
- **BaseBootstrap** (abstract MonoBehaviour): 개발자가 상속하여 부트 로직을 구현하는 베이스 클래스

---

## 2. Files (SSOT)

- `framework-cs/upm/com.devian.foundation/Editor/Settings/DevianSettingsMenu.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/BaseBootstrap.cs`

---

## 3. 경로 (SSOT)

| 에셋 | 프로젝트 경로 | Resources.Load 경로 |
|------|---------------|---------------------|
| DevianSettings | `Assets/Resources/Devian/DevianSettings.asset` | `Devian/DevianSettings` |
| 부트 컨테이너 Prefab | `Assets/Resources/Devian/Bootstrap.prefab` | `Devian/Bootstrap` |

---

## 4. BaseBootstrap 클래스

### 정적 상수

```csharp
public const string DefaultPrefabPath = "Devian/Bootstrap";
```

### 정적 상태

```csharp
private static BaseBootstrap? _instance;
private static bool _booted;

public static bool IsCreated => _instance != null;
public static bool IsBooted => _booted;
```

### 추상 메서드

```csharp
protected abstract IEnumerator OnBootProc();
```

개발자가 구현해야 하는 부팅 로직. BootProc()에서 1회만 호출된다.

### 정적 API

#### CreateFromResources

```csharp
public static bool CreateFromResources()
```

동작:
1. 이미 생성되었으면 true 반환
2. `Resources.Load<GameObject>(DefaultPrefabPath)`로 프리팹 로드
3. 프리팹이 없으면 false 반환
4. `Object.Instantiate(prefab)` 실행
5. `Object.DontDestroyOnLoad(instance)` 적용
6. 프리팹에서 `BaseBootstrap` 컴포넌트 확인 (정확히 1개 필수)
7. `_instance`에 저장 후 true 반환

#### BootProc

```csharp
public static IEnumerator BootProc()
```

동작:
1. `_booted == true`면 `yield break`
2. `_instance == null`이면 `CreateFromResources()` 시도
   - 실패 시 에러 로그 후 `yield break`
3. `CreateFromResources()` 후에도 `_instance == null`이면 에러 로그 후 `yield break`
4. `try { yield return _instance.OnBootProc(); } finally { _booted = true; }`

### Domain Reload 대응

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
{
    _instance = null;
    _booted = false;
}
```

---

## 5. BaseScene과의 통합

BaseScene은 `UseBootstrap` 프로퍼티로 Bootstrap 사용 여부를 제어한다.

### Awake: 생성 트리거

```csharp
private void Awake()
{
    OnInitAwake();
    if (UseBootstrap && !BaseBootstrap.IsCreated)
    {
        BaseBootstrap.CreateFromResources();
    }
}
```

### SceneTransManager: OnEnter 전에 BootProc 호출

```csharp
// SceneTransManager.Start() 또는 LoadSceneAsync()에서
yield return BaseBootstrap.BootProc();  // 이미 부팅이면 즉시 종료
yield return scene.OnEnter();
```

### BaseScene.Start(): OnStart 전에 BootProc 호출

```csharp
private IEnumerator Start()
{
    if (UseBootstrap)
    {
        yield return BaseBootstrap.BootProc();  // 이미 부팅이면 즉시 종료
    }
    yield return OnStart();
}
```

---

## 6. 부트 컨테이너 구조

부트 컨테이너는 **BaseBootstrap 파생 컴포넌트를 정확히 1개 포함하는 프리팹**이다:

- 개발자는 `BaseBootstrap`을 상속한 클래스를 만들어 프리팹에 부착
- `OnBootProc()`에서 초기화 로직 구현
- 추가로 필요한 Manager 컴포넌트들을 함께 부착 가능

**프레임워크가 BaseBootstrap 파생 컴포넌트를 자동 추가하지 않는다.** 개발자가 직접 추가해야 한다.

### 필수 CompoSingleton 컴포넌트

BaseBootstrap.Awake()에서 `ensureRequiredComponents()`가 호출된다.

> **UIManager는 Bootstrap 관리 대상이 아니다.** UIManager는 AutoSingleton으로 전환되어 `Instance` 접근 시 자동 생성된다.

---

## 7. Editor 메뉴

**메뉴: Devian/Create Bootstrap**

이 메뉴는 다음을 생성/보수한다:
1. DevianSettings (`Assets/Resources/Devian/DevianSettings.asset`)
2. 부트 컨테이너 Prefab (`Assets/Resources/Devian/Bootstrap.prefab`)

**부트 컨테이너 Prefab 기본 구성:**
- SceneTransManager (CompoSingleton 기반)

**BaseBootstrap 파생 컴포넌트는 개발자가 직접 추가해야 한다.**

---

## 8. Settings 접근

Settings는 Resources에서 직접 로드한다:

```csharp
var settings = Resources.Load<DevianSettings>(DevianSettings.ResourcesPath);
```

---

## 9. 테스트 규약

PlayMode 테스트는 테스트 씬에 부트 컨테이너를 배치하거나, SetUp에서 직접 instantiate 한다.

---

## 10. Reference

- Parent: `skills/devian-unity/30-unity-components/skill.md`
- DevianSettings: `skills/devian-unity/30-unity-components/23-devian-settings/SKILL.md`
- SceneTransManager: `skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md`
- Singleton: `skills/devian-unity/30-unity-components/31-singleton/SKILL.md`
- UIManager: `skills/devian-unity/40-ui-components/10-ui-manager/skill.md`
