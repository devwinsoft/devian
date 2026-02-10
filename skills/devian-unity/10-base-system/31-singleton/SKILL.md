# 31-singleton

Status: ACTIVE
AppliesTo: v10
Type: Component Specification

## 0. 목표

- 개발자가 실수 없이 싱글톤을 사용하도록 **기본 AutoSingleton**을 제공한다.
- 필요 시 **CompoSingleton**으로 배치 책임(component)을 명시한다.
- 모든 싱글톤은 **단일 저장소(SingletonRegistry)**를 통해 통합 관리한다.

---

## 1. 제공 타입 (2종)

### AutoSingleton\<T\> (기본)

`T.Instance` 접근 시:
1. Registry 조회
2. 씬/자식에서 기존 컴포넌트 탐색(비활성 포함)
3. 없으면 새 GameObject 생성 + AddComponent
4. Registry 등록 + DontDestroyOnLoad 적용

**"없으면 자동 생성"이 기본 동작이다.**

**Shutdown 억제**: 에디터 종료/플레이 종료/앱 종료 중(`IsShuttingDown == true`)에는 자동 생성이 억제되며 `Instance`는 `null`을 반환한다. Shutdown 방어가 필요하면:
- `AutoSingleton<T>.IsShuttingDown`으로 사전 체크
- `Singleton.TryGet<T>(out var t)` 또는 `T.TryGet(out var t)`로 안전 조회 (자동 생성 없음)

### CompoSingleton\<T\> (선택)

- 씬/프리팹에 컴포넌트로 붙여서 사용한다.
- `Awake()`에서 Registry에 등록한다.
- **우선순위 최고**: CompoSingleton이 등록되면 같은 타입의 Auto 인스턴스를 대체한다(Adopt).

---

## 2. 우선순위 규칙 (Hard Rule)

같은 타입 T에 대해 **Compo > Auto**가 항상 승리한다.

- CompoSingleton이 늦게 로드되어도, 기존 AutoSingleton 인스턴스를 **대체(Adopt)**해야 한다.
- CompoSingleton끼리 중복은 **즉시 실패(예외)**로 처리한다.

---

## 3. Adopt 정책 (Hard Rule)

Registry에 AutoSingleton이 등록된 상태에서 CompoSingleton이 등록되면:

1. CompoSingleton을 "정본"으로 등록
2. 기존 AutoSingleton 인스턴스는 **제거(파괴)**하여 중복을 해소
3. 이 상황은 실수 가능성이 높으므로 **Error 로그**를 남긴다 (단, 앱이 계속 진행 가능해야 함)

---

## 4. 접근 API (권장)

| API | 동작 |
|-----|------|
| `Singleton.Get<T>()` | 없으면 예외 (Fail-fast) |
| `Singleton.TryGet<T>(out T)` | 없으면 false |
| `T.Instance` | AutoSingleton/CompoSingleton이 제공하는 편의 (기본은 AutoSingleton). Shutdown 중 null 반환 |
| `AutoSingleton<T>.IsShuttingDown` | Shutdown 구간 여부 (`OnApplicationQuit` 또는 `!Application.isPlaying`) |

---

## 5. 금지 (Hard Rule)

- 기존 싱글톤 시스템(ResSingleton/SceneSingleton/MonoSingleton 등)을 새로 사용/추가하지 않는다.
- "조용히 Destroy로 중복을 숨기는 정책"을 기본값으로 두지 않는다.
- Registry를 우회하는 static instance 보관을 금지한다 (모든 인스턴스는 Registry가 SSOT).

---

## 6. 파일 위치 (SSOT)

```
com.devian.foundation/Runtime/Unity/Singletons/
├── SingletonSource.cs      # enum
├── SingletonRegistry.cs    # SSOT 저장소
├── Singleton.cs            # 정적 파사드
├── AutoSingleton.cs        # 기본 싱글톤 (컴포넌트 베이스)
└── CompoSingleton.cs       # 씬/프리팹 싱글톤 (컴포넌트 베이스)
```

---

## 7. SingletonSource enum

```csharp
public enum SingletonSource
{
    Auto = 0,   // 자동 생성 (최저 우선순위)
    Compo = 1,  // 씬/프리팹 컴포넌트 (최고 우선순위)
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
| Auto | Compo | Adopt (기존 파괴 + Error 로그) |
| Compo | Compo | **즉시 예외** (중복 금지) |
| Auto | Auto | **즉시 예외** (중복 금지) |

### 파괴 정책

- old 인스턴스가 `Component`면 `Object.Destroy(comp)`로 **컴포넌트만 제거** (GameObject 전체 파괴 금지)
- old 인스턴스가 `UnityEngine.Object`이지만 Component가 아니면 `Object.Destroy(old)`로 제거
- 순수 C# 객체면 참조만 교체 (로그 남김)

---

## 9. 사용 예시

### 기본 사용 (AutoSingleton)

```csharp
public class GameManager : AutoSingleton<GameManager>
{
    public void StartGame() { ... }
}

// 어디서든 접근 - 없으면 자동 생성
GameManager.Instance.StartGame();
```

### 씬 배치가 필요한 경우 (CompoSingleton)

```csharp
public class AudioManager : CompoSingleton<AudioManager>
{
    [SerializeField] private AudioSource _bgmSource;
}

// 씬에 배치된 인스턴스가 정본이 됨
// 만약 AutoSingleton으로 먼저 접근했어도 CompoSingleton이 대체함
```

---

## 10. Reference

- Parent: `skills/devian-core/03-ssot/SKILL.md`
- Index: `skills/devian-unity/10-base-system/skill.md`
