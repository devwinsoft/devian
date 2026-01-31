# Bootstrap Resource Object

## 0. 목적
씬과 무관하게 Devian 부팅을 강제한다. 테스트(PlayMode)에서도 동일한 부팅 파이프라인이 재사용되어야 한다.

---

## 1. 구성
- DevianBootstrap (static)
- DevianBootstrapRoot (MonoBehaviour, DevianSettings 참조 보유)
- BootCoordinator (MonoBehaviour, DDOL)
- IDevianBootStep (boot step interface)
- SceneTransManager 연동(부팅 완료 전 OnEnter 금지)

---

## 2. Files (SSOT)

- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/DevianBootstrap.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/DevianBootstrapRoot.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/BootCoordinator.cs`
- `framework-cs/upm/com.devian.foundation/Runtime/Unity/Bootstrap/IDevianBootStep.cs`
- `framework-cs/upm/com.devian.foundation/Editor/Settings/DevianSettingsMenu.cs`

---

## 3. Hard 규약

### 경로 (SSOT)

| 에셋 | 프로젝트 경로 | Resources.Load 경로 |
|------|---------------|---------------------|
| DevianSettings | `Assets/Resources/Devian/DevianSettings.asset` | `Devian/DevianSettings` |
| BootstrapRoot Prefab | `Assets/Resources/Devian/BootstrapRoot.prefab` | `Devian/BootstrapRoot` |

### 부팅 규약

- Bootstrap은 씬(0번)으로 보장하지 않는다.
- DevianBootstrap.Ensure()는 Bootstrap Root를 DDOL로 보장한다.
- Prefab이 존재하면 Instantiate하여 사용한다.
- Prefab이 없으면 fallback으로 Bootstrap Root를 코드로 생성한다(테스트/최소 실행 보장).
- BootCoordinator는 1회 부팅하며 `IsBooted`로 완료를 신호한다.
- BootCoordinator는 같은 루트에 있는 IDevianBootStep들을 Order 오름차순으로 실행한다.
- SceneTransManager는 Boot 완료 전에는 첫 BaseScene.OnEnter()를 호출하지 않는다.
- Boot 실패 시 예외/에러로 즉시 노출한다(조용히 무시 금지).

### Settings 로딩 규약

- DevianSettings는 Resources에서 로드한다 (`Resources.Load<DevianSettings>("Devian/DevianSettings")`)
- DevianBootstrap.Settings는 캐시하며, 다음 우선순위로 로드:
  1. BootstrapRoot.Settings가 있으면 사용
  2. 없으면 Resources.Load로 직접 로드
- BootstrapRoot.Settings가 null이면 Resources에서 로드하여 자동 주입

---

## 4. DevianBootstrapRoot

Resources BootstrapRoot prefab의 루트 컴포넌트.
DevianSettings를 참조로 보유할 수 있으나, 없어도 Resources 로드로 대체된다.

```csharp
public sealed class DevianBootstrapRoot : MonoBehaviour
{
    [SerializeField] private DevianSettings? _settings;
    public DevianSettings? Settings => _settings;
    public void SetSettings(DevianSettings? settings) { _settings = settings; }
}
```

**접근 방법:**
```csharp
// DevianBootstrap.Settings로 접근 (캐시됨)
var settings = DevianBootstrap.Settings;

// 또는 직접 Resources.Load
var settings = Resources.Load<DevianSettings>("Devian/DevianSettings");
```

---

## 5. API
- DevianBootstrap.Ensure()
- DevianBootstrap.WaitUntilBooted()
- DevianBootstrap.IsBooted
- DevianBootstrap.Settings (Resources에서 로드, 캐시됨)
- BootCoordinator.IsBooted / BootCoordinator.WaitUntilBooted()
- BootCoordinator.BootError
- BootCoordinator.Booted (event)
- IDevianBootStep.Order / Boot()

---

## 6. 부팅 순서 표

| 단계 | 트리거 | 주체 | 조건 | 수행 내용 | 결과 |
|------|--------|------|------|-----------|------|
| 0 | 앱 시작 | DevianBootstrap (static) | BeforeSceneLoad | Bootstrap Root 존재 보장, BootCoordinator 생성/활성, Settings 주입 | Bootstrap Root가 DDOL로 유지 |
| 1 | Bootstrap Root 생성 직후 | BootCoordinator.Awake | 동기 | autoStart=true면 StartBoot() 호출 | Boot 시작 |
| 2 | StartBoot() | BootCoordinator._BootRoutine | 코루틴 | IDevianBootStep들을 Order 순으로 실행 | IsBooted=true |
| 3 | 첫 씬 로드 완료 후 | SceneTransManager.Start | Booted 대기 | Booted 이후 현재 ActiveScene의 BaseScene.OnEnter 1회 보장 | 첫 씬 Enter 완료 |
| 4 | 이후 전환 | SceneTransManager.LoadSceneAsync | 전환 중 방지 | FadeOut→OnExit→Load→OnEnter→FadeIn | 안정적 씬 전환 |

---

## 7. Editor 메뉴

**메뉴: Devian/Create Bootstrap**

이 메뉴는 다음을 생성/보수한다:
1. DevianSettings (`Assets/Resources/Devian/DevianSettings.asset`)
2. BootstrapRoot Prefab (`Assets/Resources/Devian/BootstrapRoot.prefab`)

**마이그레이션:**
- 기존 `Assets/Settings/DevianSettings.asset`가 있으면 `Assets/Resources/Devian/`로 자동 이동

**BootstrapRoot Prefab 구성:**
- DevianBootstrapRoot (Settings 참조 연결)
- BootCoordinator
- SceneTransManager

---

## 8. 테스트 규약
- PlayMode 테스트는 SetUp에서 `DevianBootstrap.Ensure()` 호출 후 필요 시 `yield return DevianBootstrap.WaitUntilBooted()`로 부팅을 강제한다.

---

## 9. Reference

- Parent: `skills/devian-unity/30-unity-components/SKILL.md`
- DevianSettings: `skills/devian-unity/30-unity-components/23-devian-settings/SKILL.md`
- SceneTransManager: `skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md`
