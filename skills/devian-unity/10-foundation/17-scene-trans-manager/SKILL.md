# 17-scene-trans-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

Scene 전환 파이프라인을 단일화(직렬화)하고, 씬별 초기화/정리를 SceneBase 훅으로 분리한다.

---

## Files (SSOT)

- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Scene/SceneBase.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Scene/SceneTransManager.cs`

---

## Concepts

### SceneBase

씬 라이프사이클 훅을 제공하는 추상 베이스 클래스. Bootstrap과 무관한 순수 라이프사이클만 담당한다.
씬 루트(또는 유일한 오브젝트)에 1개만 존재하는 것을 권장한다. 2개 이상이면 경고 로그를 출력하고 첫 번째만 사용한다.

**라이프사이클 훅:**

| 훅 | 호출 시점 | 호출 주체 | 용도 |
|----|----------|----------|------|
| `onInitAwake()` | Unity Awake()에서 항상 1회 | SceneBase.Awake | 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업 |
| `Enter()` → `onEnter()` | 씬 진입 시 (전환 또는 부팅) | SceneTransManager | 씬 진입 상태 초기화 |
| `onStart()` | Unity Start 시점 | SceneBase.Start | 씬 시작 로직 |
| `Exit()` → `onExit()` | 전환으로 이탈 시 | SceneTransManager | 정리 작업 |

**Template Method 패턴:**
- `Enter()` / `Exit()` — public 파사드. SceneTransManager가 호출한다.
- `onEnter()` / `onExit()` / `onStart()` / `onInitAwake()` — protected 훅. 파생 클래스가 구현한다.

### SceneTransManager

**CompoSingleton 기반 전역 흐름 제어자**로, Scene 전환을 직렬화(동시 전환 방지)한다.
Bootstrap prefab에 포함되어 부팅 시 자동 등록된다.

**책임:**
- 전환 흐름 직렬화 (동시 전환 방지)
- Fade 시간 전달 (이벤트 위임)
- SceneBase Exit/Enter 호출
- Hook(beforeUnload/afterLoad) 실행

**전환 순서 (LoadSceneAsync):**
1. FadeOutRequested 이벤트 발생 (fadeOutSeconds > 0인 경우)
2. beforeUnload 훅 실행 (있으면)
3. 현재 씬의 `SceneBase.Exit()` 호출
4. `AssetManager.LoadSceneAsync()` 로 새 씬 로드
5. afterLoad 훅 실행 (있으면)
6. `BaseBootstrap.Instance.BootProc()` 호출 (이미 부팅이면 즉시 종료)
7. 새 씬의 `SceneBase.Enter()` 호출
8. FadeInRequested 이벤트 발생 (fadeInSeconds > 0인 경우)

**특징:**
- `CompoSingleton<SceneTransManager>` 상속 (Bootstrap prefab에 포함)
- `_isTransitioning` 플래그로 중복 전환 방지
- Delegate 기반 Hook (beforeUnload, afterLoad)
- 페이드는 이벤트로 위임 (FadeOutRequested, FadeInRequested)

### 페이드 위임 (Hard Rule)

**SceneTransManager는 페이드 UI(CanvasGroup/Overlay)를 직접 소유하지 않는다.**

페이드 처리는 외부 컴포넌트가 이벤트를 구독하여 처리한다:

```csharp
// 페이드 이벤트
public event Func<float, Task>? FadeOutRequested;
public event Func<float, Task>? FadeInRequested;
```

- 구독자는 fadeSeconds 동안 페이드를 수행하는 Task를 반환한다.
- 여러 구독자가 있으면 등록 순서대로 순차 실행된다.
- fadeSeconds가 0 이하면 이벤트 호출을 스킵한다.

### 부팅 씬 (첫 씬) 처리

SceneTransManager는 `Start()`에서 Active Scene의 SceneBase를 찾아:
- `BaseBootstrap.Instance.BootProc()`를 호출한다 (이미 부팅이면 즉시 종료)
- `Enter()`를 호출한다
- onStart는 SceneBase.Start()에서 호출된다

### Additive 모드

- Additive 모드 로드는 가능하지만, ActiveScene 선택은 best-effort로 처리한다.
- Single 모드면 Unity가 활성 씬을 전환하므로 자동으로 처리된다.

---

## API

### LoadSceneAsync

```csharp
public async Task LoadSceneAsync(
    string sceneKey,
    LoadSceneMode mode = LoadSceneMode.Single,
    float fadeOutSeconds = 0.2f,
    float fadeInSeconds = 0.2f,
    Func<Task>? beforeUnload = null,
    Func<Task>? afterLoad = null,
    Action<string>? onError = null)
```

| 파라미터 | 설명 | 기본값 |
|----------|------|--------|
| sceneKey | Addressables 씬 키 | (필수) |
| mode | LoadSceneMode.Single 또는 Additive | Single |
| fadeOutSeconds | 페이드 아웃 시간 (0 이하면 스킵) | 0.2f |
| fadeInSeconds | 페이드 인 시간 (0 이하면 스킵) | 0.2f |
| beforeUnload | 언로드 전 실행할 Task | null |
| afterLoad | 로드 후 실행할 Task | null |
| onError | 에러 콜백 | null |

---

## Error/Log

- 로깅은 `Devian.Log` 사용 (메시지 1개만, 필요하면 ex 문자열 포함)
- 실패 시 `Devian.Log.Error("...")`
- SceneBase가 여러 개 발견되면 `Devian.Log.Warn(...)` 로 경고 후 첫 번째 사용

---

## Usage Example

### 기본 사용 (LoadSceneAsync)

```csharp
// 기본 전환 (페이드 0.2초)
await SceneTransManager.Instance.LoadSceneAsync("SceneKey_Main");

// 페이드 시간 커스텀
await SceneTransManager.Instance.LoadSceneAsync(
    "SceneKey_Game",
    LoadSceneMode.Single,
    fadeOutSeconds: 0.5f,
    fadeInSeconds: 0.3f
);
```

### Hook 사용 (beforeUnload/afterLoad)

```csharp
await SceneTransManager.Instance.LoadSceneAsync(
    "SceneKey_Game",
    LoadSceneMode.Single,
    fadeOutSeconds: 0.25f,
    fadeInSeconds: 0.25f,
    beforeUnload: () => SaveBeforeLeaveAsync(),
    afterLoad: () => WarmupAfterEnterAsync()
);

async Task SaveBeforeLeaveAsync()
{
    // 씬 떠나기 전 저장 로직
    await Task.CompletedTask;
}

async Task WarmupAfterEnterAsync()
{
    // 씬 로드 후 워밍업 로직
    await Task.CompletedTask;
}
```

### 페이드 위임 (외부 컴포넌트가 구독)

```csharp
// 페이드 UI 컴포넌트에서 구독
void OnEnable()
{
    SceneTransManager.Instance.FadeOutRequested += OnFadeOut;
    SceneTransManager.Instance.FadeInRequested += OnFadeIn;
}

void OnDisable()
{
    if (Singleton.TryGet<SceneTransManager>(out var stm))
    {
        SceneTransManager.Instance.FadeOutRequested -= OnFadeOut;
        SceneTransManager.Instance.FadeInRequested -= OnFadeIn;
    }
}

async Task OnFadeOut(float seconds)
{
    // CanvasGroup alpha를 0 → 1로 변경
    float t = 0f;
    while (t < seconds)
    {
        t += Time.unscaledDeltaTime;
        _canvasGroup.alpha = Mathf.Clamp01(t / seconds);
        await Task.Yield();
    }
    _canvasGroup.alpha = 1f;
}

async Task OnFadeIn(float seconds)
{
    // CanvasGroup alpha를 1 → 0로 변경
    float t = 0f;
    while (t < seconds)
    {
        t += Time.unscaledDeltaTime;
        _canvasGroup.alpha = 1f - Mathf.Clamp01(t / seconds);
        await Task.Yield();
    }
    _canvasGroup.alpha = 0f;
}
```

### SceneBase 구현 예시

Bootstrap 통합이 필요하면 contents 레이어에서 SceneBase를 상속하여 직접 구현한다 (프레임워크는 SceneBoot을 제공하지 않는다).

```csharp
public class MainScene : SceneBase
{
    // 전환과 무관하게 항상 1회 호출 (Unity Awake 시점)
    protected override void onInitAwake()
    {
        // 레퍼런스 캐싱, 컴포넌트 연결 등
    }

    // 씬 진입 시 호출 (SceneTransManager가 Enter() 호출)
    protected override async Task onEnter()
    {
        // 씬 진입 시 초기화 로직
        await Task.CompletedTask;
    }

    // Unity Start 시점에 호출 (SceneBase.Start()가 호출)
    protected override async Task onStart()
    {
        // 씬 시작 로직
        await Task.CompletedTask;
    }

    // 전환으로 이탈 시 호출 (SceneTransManager가 Exit() 호출)
    protected override async Task onExit()
    {
        // 씬 퇴장 시 정리 로직
        await Task.CompletedTask;
    }
}
```

---

## Non-goals

- **페이드 UI 직접 소유**: SceneTransManager는 CanvasGroup/Overlay를 소유하지 않는다. 이벤트로 위임한다.
- 로딩 UI / 프로그레스바 / 다운로드 진행률 표시는 이번 범위 밖이다.
- DI / ServiceLocator 설계 도입은 포함하지 않는다.
- Addressables 라벨/키 정책 변경은 포함하지 않는다.

---

## Bootstrap Prefab에 포함

SceneTransManager는 Bootstrap prefab에 포함된다.
이로써 부팅과 씬 전환이 일관된 파이프라인으로 동작한다.

Bootstrap prefab에는 BaseBootstrap 파생 컴포넌트가 정확히 1개 필요하다 (개발자가 직접 추가).

---

## Reference

- Parent: `skills/devian-unity/10-foundation/SKILL.md`
- AssetManager: `skills/devian-unity/10-foundation/18-asset-manager/SKILL.md`
- Singleton: `skills/devian-unity/10-foundation/15-singleton/SKILL.md`
- Bootstrap: `skills/devian-unity/10-foundation/16-bootstrap/SKILL.md`
