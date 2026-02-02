# Bootstrap Resource Object

## 0. 목적

BootstrapRoot(부트 컨테이너)를 정의한다. Devian 프레임워크는 자동 instantiate를 제공하지 않는다.

---

## 1. 구성

- **BootstrapRoot prefab**: 부트 컨테이너 프리팹 (예: `Assets/Resources/Devian/BootstrapRoot.prefab`)
- **BootSingletonBase** (abstract MonoBehaviour): 부트 컨테이너 식별/탐색용 마커 베이스 (31-singleton 참조)

---

## 2. Files (SSOT)

- `framework-cs/upm/com.devian.foundation/Editor/Settings/DevianSettingsMenu.cs`

---

## 3. 경로 (SSOT)

| 에셋 | 프로젝트 경로 | Resources.Load 경로 |
|------|---------------|---------------------|
| DevianSettings | `Assets/Resources/Devian/DevianSettings.asset` | `Devian/DevianSettings` |
| BootstrapRoot Prefab | `Assets/Resources/Devian/BootstrapRoot.prefab` | `Devian/BootstrapRoot` |

---

## 4. BootstrapRoot 구조

BootstrapRoot는 **부트 컨테이너 프리팹**이다:
- 여기에 `BootSingleton<T>` 컴포넌트들을 붙여서 등록
- 사용자는 자신의 초기화 MonoBehaviour 스크립트를 붙여서, 원하는 로딩/초기화/등록을 직접 코딩

**프레임워크는 BootstrapRoot를 자동 instantiate 하지 않는다.** 개발자 코드가 부팅 흐름을 책임진다.

**부트 컨테이너 식별:**
- `BootSingletonBase`를 `FindAnyObjectByType`으로 탐색하여 부트 컨테이너 존재 여부 판단
- `BootSingleton<T>`는 `BootSingletonBase`를 상속하므로, 어떤 BootSingleton이든 있으면 부트 컨테이너가 로드된 것

**부트 컨테이너에는 최소 1개 이상의 BootSingleton 기반 컴포넌트를 붙이는 것을 권장한다.**

---

## 5. 프로젝트 적용 방식

Devian은 자동 부팅 코드를 제공하지 않는다. 프로젝트에서 다음 중 하나를 선택한다:

### A) 첫 씬에 BootstrapRoot.prefab 배치 (권장)

1. 첫 씬에 BootstrapRoot.prefab을 직접 배치
2. BootSingleton 기반 컴포넌트들이 Awake()에서 Registry에 등록됨
3. 필요 시 DontDestroyOnLoad 적용

### B) 사용자 코드에서 직접 instantiate

```csharp
// 예시: 사용자 초기화 코드에서 직접 로드
var prefab = Resources.Load<GameObject>("Devian/BootstrapRoot");
var go = Object.Instantiate(prefab);
Object.DontDestroyOnLoad(go);
```

---

## 6. Editor 메뉴

**메뉴: Devian/Create Bootstrap**

이 메뉴는 다음을 생성/보수한다:
1. DevianSettings (`Assets/Resources/Devian/DevianSettings.asset`)
2. BootstrapRoot Prefab (`Assets/Resources/Devian/BootstrapRoot.prefab`)

**BootstrapRoot Prefab 기본 구성:**
- SceneTransManager (CompoSingleton 기반)

**DevianSettings는 별도로 Resources에 생성/보수한다.** BootstrapRoot prefab은 Settings를 참조하지 않는다.

사용자는 BootstrapRoot.prefab에 초기화 스크립트를 추가로 부착해, 원하는 순서/로딩/등록을 직접 코딩할 수 있다.

---

## 7. Settings 접근

Settings는 Resources에서 직접 로드한다:

```csharp
var settings = Resources.Load<DevianSettings>(DevianSettings.ResourcesPath);
```

---

## 8. 테스트 규약

PlayMode 테스트는 테스트 씬에 BootstrapRoot를 배치하거나, SetUp에서 직접 instantiate 한다.

---

## 9. Reference

- Parent: `skills/devian-unity/30-unity-components/SKILL.md`
- DevianSettings: `skills/devian-unity/30-unity-components/23-devian-settings/SKILL.md`
- SceneTransManager: `skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md`
- Singleton: `skills/devian-unity/30-unity-components/31-singleton/SKILL.md`
