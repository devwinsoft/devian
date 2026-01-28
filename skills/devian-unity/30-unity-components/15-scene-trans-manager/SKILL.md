# 15-scene-trans-manager

Status: ACTIVE
AppliesTo: v10
Type: Component

## Purpose

Scene 전환 파이프라인을 단일화(직렬화)하고, 씬별 초기화/정리를 BaseScene 훅으로 분리한다.

---

## Files (SSOT)

- `framework-cs/upm/com.devian.unity/Runtime/Scene/BaseScene.cs`
- `framework-cs/upm/com.devian.unity/Runtime/Scene/SceneTransManager.cs`

---

## Concepts

### BaseScene

씬 루트(또는 유일한 오브젝트)에 1개만 존재하는 것을 권장한다. 2개 이상이면 경고 로그를 출력하고 첫 번째만 사용한다.

**라이프사이클 훅:**

| 훅 | 호출 시점 | 용도 |
|----|----------|------|
| `OnInitAwake()` | Unity Awake()에서 항상 1회 | 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업 |
| `OnEnter()` | 씬 진입 시 (전환 또는 부팅) | 씬 진입 상태 초기화. **한 씬 인스턴스 당 1회만 실행** (중복 방지) |
| `OnExit()` | 전환으로 이탈 시 | 정리 작업 |

### SceneTransManager

MonoBehaviour 싱글턴으로, Scene 전환을 직렬화(동시 전환 방지)한다.

**전환 순서:**
1. overlay 페이드 아웃 (선택)
2. 현재 씬의 `BaseScene.OnExit()` 호출
3. `AssetManager.LoadSceneAsync()` 로 새 씬 로드
4. 새 씬의 `BaseScene.OnEnter()` 호출 (중복 방지)
5. overlay 페이드 인 (선택)

**특징:**
- `_isTransitioning` 플래그로 중복 전환 방지
- `CanvasGroup` 기반 overlay 페이드 (optional)
- `SceneTransOptions` 구조체로 옵션 지정

### 부팅 씬 (첫 씬) 처리

SceneTransManager는 `Start()`에서 Active Scene의 BaseScene을 찾아:
- 아직 Enter되지 않았다면 `OnEnter()`를 1회 호출한다 (bootstrap).
- 이로써 TransitionTo를 거치지 않는 첫 씬도 `OnEnter()`를 받는다.

### 중복 호출 방지

- `BaseScene.HasEntered` 플래그로 `OnEnter()`는 한 번만 호출된다.
- 전환/부팅 모두 동일 규칙이 적용된다.
- 이미 Enter된 씬에 대해 `OnEnter()`를 다시 호출하려 하면 경고 로그와 함께 스킵된다.

### Additive 모드

- 이번 초안에서는 Additive 모드 로드는 가능하지만, ActiveScene 선택은 best-effort로 처리한다.
- Single 모드면 Unity가 활성 씬을 전환하므로 자동으로 처리된다.

---

## Error/Log

- 로깅은 `Devian.Log` 사용 (메시지 1개만, 필요하면 ex 문자열 포함)
- 실패 시 `Devian.Log.Error("...")`
- BaseScene이 여러 개 발견되면 `Devian.Log.Warn(...)` 로 경고 후 첫 번째 사용
- OnEnter 중복 호출 시도 시 `Devian.Log.Warn(...)` 로 경고 후 스킵

---

## Usage Example

```csharp
// Single 씬 전환 (기본)
// Note: SceneTransManager가 씬에 배치되어 있어야 Instance가 유효함
StartCoroutine(SceneTransManager.Instance.TransitionTo(
    "SceneKey_Main",
    SceneTransManager.SceneTransOptions.DefaultSingle
));

// 커스텀 옵션
var options = new SceneTransManager.SceneTransOptions
{
    Mode = LoadSceneMode.Single,
    ActivateOnLoad = true,
    Priority = 100,
    UseFade = true,
    BlockInput = true,
    FadeOutSecondsOverride = 0.5f,
    FadeInSecondsOverride = 0.3f,
};
StartCoroutine(SceneTransManager.Instance.TransitionTo("SceneKey_Game", options));
```

### BaseScene 구현 예시

```csharp
public class MainScene : BaseScene
{
    // 전환과 무관하게 항상 1회 호출 (Unity Awake 시점)
    protected override void OnInitAwake()
    {
        // 레퍼런스 캐싱, 컴포넌트 연결 등
    }

    // 씬 진입 시 1회 호출 (전환 또는 부팅)
    public override IEnumerator OnEnter()
    {
        // 씬 진입 시 초기화 로직
        yield return null;
    }

    // 전환으로 이탈 시 호출
    public override IEnumerator OnExit()
    {
        // 씬 퇴장 시 정리 로직
        yield return null;
    }
}
```

---

## Non-goals

- 로딩 UI / 프로그레스바 / 다운로드 진행률 표시는 이번 초안 범위 밖이다.
- DI / ServiceLocator 설계 도입은 포함하지 않는다.
- Addressables 라벨/키 정책 변경은 포함하지 않는다.

---

## Reference

- Parent: `skills/devian-unity/30-unity-components/SKILL.md`
- AssetManager: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md`
