# CompoSingleton / AutoSingleton — OnDestroy → onDestroy 훅 패턴 변경

## 변경 의도

`protected virtual void OnDestroy()`를 override하는 패턴은 하위 클래스가 `base.OnDestroy()`를 누락하면 `Singleton.Unregister()`가 호출되지 않는 버그가 발생한다. (실제로 `GameNetManager`가 이 버그를 가지고 있음)

기존 `Awake()` / `onInitAwake()` 패턴과 동일하게 변경하여, base의 `Singleton.Unregister()` 호출을 보장한다.

## 변경 내용

### 1단계: Base 클래스 수정 (CompoSingleton, AutoSingleton 1-param)

**CompoSingleton\<T\>** — `OnDestroy()` 변경:
```csharp
// Before
protected virtual void OnDestroy()
{
    Singleton.Unregister((T)(object)this);
}

// After
protected void OnDestroy()
{
    onDestroy();
    Singleton.Unregister((T)(object)this);
}

protected virtual void onDestroy() { }
```

**AutoSingleton\<T\>** — 동일한 변경.

> 2-param (`CompoSingleton2`, `AutoSingleton2`)은 static class이므로 변경 대상 아님.

**파일 (UPM 2-path mirror):**
| # | 파일 | 역할 |
|---|------|------|
| 1 | `upm/com.devian.domain.common/.../CompoSingleton.cs` | 정본 |
| 2 | `apps/.../Packages/com.devian.domain.common/.../CompoSingleton.cs` | sync |
| 3 | `upm/com.devian.domain.common/.../AutoSingleton.cs` | 정본 |
| 4 | `apps/.../Packages/com.devian.domain.common/.../AutoSingleton.cs` | sync |

### 2단계: 하위 클래스 수정

모든 하위 클래스: `override void OnDestroy()` → `override void onDestroy()`, `base.OnDestroy()` 제거.

| # | 클래스 | Base | 변경 내용 | 경로 수 |
|---|--------|------|-----------|---------|
| 1 | TableManager | AutoSingleton | cleanup 로직 유지, `base.OnDestroy()` 제거 | 2 (UPM mirror) |
| 2 | SoundManager | AutoSingleton | cleanup 로직 유지, `base.OnDestroy()` 제거 | 2 (UPM mirror) |
| 3 | VoiceManager | AutoSingleton | cleanup 로직 유지, `base.OnDestroy()` 제거 | 2 (UPM mirror) |
| 4 | GameNetManager | CompoSingleton | cleanup 로직 유지 (기존에 `base.OnDestroy()` 누락 — 버그 수정됨) | 1 (Test only) |
| 5 | AchieveManager | CompoSingleton | cleanup 로직 유지, `base.OnDestroy()` 제거 | 3 (Samples~ 3-path) |
| 6 | PurchaseManager | CompoSingleton | cleanup 로직 유지, `base.OnDestroy()` 제거 | 3 (Samples~ 3-path) |
| 7 | MissionManager | CompoSingleton | override 전체 제거 (커스텀 로직 없이 `base.OnDestroy()`만 호출하고 있었음) | 3 (Samples~ 3-path) |
| 8 | VirtualGamepadDriver | CompoSingleton | device cleanup 유지, `base.OnDestroy()` 제거 | 3 (Samples~ 3-path) |

> **UICanvasSample**은 `UICanvas<T>` 상속 (Singleton 아님) — **범위 밖**.

### 3단계: 스킬 문서 수정

`skills/devian-unity/20-domain-common-system/29-singleton/SKILL.md`:
- §1의 AutoSingleton/CompoSingleton 설명에 `onDestroy()` 훅 언급 추가 (기존 `onInitAwake()` 설명과 대칭)

## 수정 파일 총 수

- Base 클래스: 4파일
- 하위 클래스: 19파일 (UPM 6 + Test 1 + Samples 12)
- 스킬 문서: 1파일
- **합계: 24파일**
