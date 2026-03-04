# 14-application-manager

## 0. 목적

부트 컨테이너 프리팹과 ApplicationManager 추상 클래스를 정의한다.

**프레임워크는 Bootstrap prefab 생성이나 `BootProc()` 호출을 자동 처리하지 않는다.**
Bootstrap 생성과 `BootProc()` 호출은 app/contents layer 책임이다.

---

## 1. 구성

- **부트 컨테이너 프리팹**: 앱 레이어가 경로와 프리팹 구조를 결정한다. 프레임워크는 경로를 강제하지 않는다.
- **ApplicationManager** (abstract MonoBehaviour): 개발자가 상속하여 부트 로직을 구현하는 베이스 클래스

프리팹 생성 규칙은 [11-mobile-application](../../50-mobile-system/11-mobile-application/SKILL.md)에서 다룬다.

---

## 2. Files (SSOT)

- `framework-cs/upm/com.devian.domain.common/Runtime/Unity/Bootstrap/ApplicationManager.cs`

---

## 3. ApplicationManager 클래스

### 인스턴스 필드

```csharp
[SerializeField] private VersionNumber _appVersion;
public VersionNumber AppVersion => _appVersion;
```

앱 버전을 Inspector에서 설정한다. `VersionNumber`(`Major.Minor.Patch`)는 `Devian.Core`에 정의된 struct다.
버전 체크 로직(`VersionCheck`)은 `MobileApplication`(50-mobile-system/11-mobile-application)에 위치한다.

### 정적 상태

```csharp
private static ApplicationManager _instance;
private static bool _booted;

public static ApplicationManager Instance => _instance;
public static bool IsApplicationQuitting { get; private set; }
public static bool IsShuttingDown { get; private set; }
```

`_instance`는 `Awake()`에서 등록된다. Bootstrap 프리팹의 생성(Instantiate + DontDestroyOnLoad 포함 여부)은 app/contents layer가 명시적으로 담당한다.

**IsApplicationQuitting**: Unity `Application.quitting` 이벤트로 설정된다. 인스턴스 없이도 동작한다 (`[RuntimeInitializeOnLoadMethod]`로 정적 구독). `Singleton.IsShuttingDown` 등 외부 시스템이 앱 종료 상태를 판단할 때 사용한다.

**IsShuttingDown**: `OnApplicationQuit()` 및 `OnDestroy()`에서 `true`로 설정된다. 에디터 종료/플레이 종료/씬 종료 정리 단계에서 싱글톤/매니저 접근을 스킵하는 데 사용한다.

### 추상 메서드

```csharp
protected abstract Task OnBootProc();
```

개발자가 구현해야 하는 부팅 로직. BootProc()에서 1회만 호출된다.

### Foreground / Background Hook

```csharp
protected virtual void OnEnterForeground() {}
protected virtual void OnEnterBackground() {}
```

의미:
- `ApplicationManager`이 Unity `OnApplicationPause(bool)` / `OnApplicationFocus(bool)`를 수신한다.
- 내부에서 foreground 상태 변화를 dedupe한 뒤 semantic hook을 호출한다.
- app/contents layer는 Unity raw callback을 직접 override하지 말고 `OnEnterForeground()` / `OnEnterBackground()`를 override한다.
- 대표 사용처:
  - 포그라운드 복귀 시 서버 시간 재동기화
  - App Open 광고 gating
  - 일시 정지/복귀 telemetry

### BootProc (인스턴스 메서드)

```csharp
public async Task BootProc()
```

동작:
1. `_booted == true`면 즉시 return
2. `_booted = true`
3. `await OnBootProc()`

의미:
- `OnBootProc()` 예외는 상위 호출자까지 그대로 전파된다.
- 한번 시도한 `BootProc()`는 실패해도 재시도하지 않는다.
- Bootstrap 오류로 앱 시작이 중단되는 것은 정상 동작이다.

### Application.quitting 구독

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void SubscribeQuitting()
{
    Application.quitting += onQuitting;
}

private static void onQuitting()
{
    IsApplicationQuitting = true;
}
```

`Application.quitting`은 정적 이벤트로, MonoBehaviour 인스턴스 없이도 동작한다. `BeforeSceneLoad` 타이밍에 구독하여 도메인 리로드 후에도 항상 재구독된다.

### Domain Reload 대응

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
{
    _instance = null;
    _booted = false;
    IsApplicationQuitting = false;
    IsShuttingDown = false;
}
```

---

## 4. 호출 책임

프레임워크 `SceneTransManager`는 `BootProc()`를 호출하지 않는다.
Bootstrap은 app/contents layer가 명시적으로 생성하고 실행해야 한다.

예시 (Resources.Load 사용 시):

```csharp
var app = Singleton.CreateFromResources<ApplicationManager, TestApplication>("Devian/Bootstrap");
await app.BootProc();
```

> `"Devian/Bootstrap"` 경로는 예시이다. 앱 레이어가 프리팹 위치를 자유롭게 결정한다.

foreground resume 처리 예시:

```csharp
protected override void OnEnterForeground()
{
    // 서버 시간 재동기화, resume 처리 등
}
```

샘플 구현도 같은 구조다:
- bootstrap 생성: `UnityExample/Assets/Scripts/Test/TestApplication.cs` — `TestApplication.Create()`
- boot 실행 보장: `UnityExample/Assets/Scripts/Test/TestSceneBootstrap.cs` — `onStart()`

---

## 5. 부트 컨테이너 구조

부트 컨테이너는 **ApplicationManager 파생 컴포넌트를 정확히 1개 포함하는 프리팹**이다:

- 개발자는 `ApplicationManager`을 상속한 클래스를 만들어 프리팹에 부착
- `OnBootProc()`에서 초기화 로직 구현
- 추가로 필요한 Manager 컴포넌트들을 함께 부착 가능

**프레임워크가 ApplicationManager 파생 컴포넌트를 자동 추가하지 않는다.** 개발자가 직접 추가해야 한다.
**프레임워크가 Bootstrap prefab을 자동 생성하지도 않는다.**

### 필수 컴포넌트 부착

ApplicationManager.Awake()에서 `ensureRequiredComponents()`가 호출된다.

- ApplicationManager 기본 구현은 필수 매니저를 추가하지 않는다.
- `CompoSingleton` 계열은 런타임 `AddComponent`로 만들지 않고, bootstrap prefab/scene object에 미리 부착해야 한다.
- 소비자 모듈/도메인은 파생 bootstrap에서 필요한 컴포넌트가 이미 부착되었는지 검증/보장한다.

---

## 6. 테스트 규약

PlayMode 테스트는 테스트 씬에 부트 컨테이너를 배치하거나, SetUp에서 직접 instantiate 한다.

---

## 7. Reference

- Parent: `skills/devian-unity/11-common-system/SKILL.md`
- SceneTransManager: `skills/devian-unity/11-common-system/28-scene-trans-manager/SKILL.md`
- Singleton: `skills/devian-unity/11-common-system/29-singleton/SKILL.md`
