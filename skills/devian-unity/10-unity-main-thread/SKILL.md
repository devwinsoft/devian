# 10-unity-main-thread

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Unity 메인스레드 강제 유틸(`UnityMainThread`, `UnityMainThreadDispatcher`)의 규약을 정의한다.

---

## 목표

- Unity API 호출이 반드시 메인 스레드에서만 수행되도록 보장하는 유틸리티 제공
- 백그라운드 스레드에서 메인 스레드로 작업을 디스패치하는 큐 시스템 제공
- 빌더가 `_Shared` 폴더를 clean+generate 하여 결정적 빌드 보장

---

## 산출물 (빌더 생성)

| 파일 | 설명 |
|------|------|
| `com.devian.unity/Runtime/_Shared/UnityMainThread.cs` | 메인 스레드 감지 헬퍼 |
| `com.devian.unity/Runtime/_Shared/UnityMainThreadDispatcher.cs` | 백그라운드→메인 스레드 디스패처 |

> **Hard Rule:** `Runtime/_Shared`는 **생성 전용 영역**이다. 빌더가 clean 후 generate 한다. 수동 편집/보존 금지.

---

## 규약

### UnityMainThread

- `internal static class`
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`로 메인 스레드 ID 캡처
- `IsMainThread` 속성: 현재 스레드가 메인 스레드인지 반환
- `EnsureOrThrow(string context)`: 메인 스레드가 아니면 예외 발생

### UnityMainThreadDispatcher

- `internal sealed class : MonoBehaviour`
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`로 자동 생성
- 숨김 GameObject + `DontDestroyOnLoad` + 중복 생성 방지
- `ConcurrentQueue<LogItem>` 사용 (스레드 안전)
- `Enqueue(LogItem)`: 백그라운드 스레드에서 호출 가능
- `Update()`에서 큐 소비 → `Debug.Log*` 호출
- **maxPerFrame 제한 (500)**: 프레임당 최대 처리량 제한으로 폭주 방지

### LogItem

- `internal readonly struct`
- 필드: `Level`, `Tag`, `Message`, `ExceptionText`

---

## 의존 관계

```
UnityMainThread      ← Singleton, Pool, PoolManager (EnsureOrThrow 사용)
UnityMainThreadDispatcher ← UnityLogSink (백그라운드 로그 디스패치)
```

---

## 빌더 동작

1. `generateUnitySharedRuntime(stagingUpm)` 호출
2. `Runtime/_Shared` 폴더 **clean** (rm -rf)
3. 2개 파일 생성:
   - `UnityMainThread.cs`
   - `UnityMainThreadDispatcher.cs`
4. 로그 출력: `[OK] Generated unity shared: Runtime/_Shared (2 files)`

---

## DoD (완료 정의) — Hard Gate

- [ ] 빌드 후 `com.devian.unity/Runtime/_Shared/UnityMainThread.cs` 존재
- [ ] 빌드 후 `com.devian.unity/Runtime/_Shared/UnityMainThreadDispatcher.cs` 존재
- [ ] UPM 경로와 UnityExample 경로 모두 동일하게 생성됨
- [ ] Dispatcher는 `maxPerFrame` 제한(500)을 가짐
- [ ] Unity API 호출(`Debug.Log*`)은 메인 스레드에서만 수행됨
- [ ] 빌더 로그가 `(2 files)`로 출력됨

**FAIL 조건:**

- `_Shared` 폴더에 수동 파일이 남아있음 (clean 정책 위반)
- `UnityMainThreadDispatcher`가 `_Shared` 밖에 위치함
- 빌더가 1개 파일만 생성함

---

## 금지

- `_Shared` clean 정책 우회 금지
- Dispatcher를 별도 패키지로 분리 금지
- `UnityMainThread` 또는 `UnityMainThreadDispatcher`를 `_Shared` 밖으로 이동 금지
- 수동 편집/보존 금지 (빌더 생성만 허용)

---

## Reference

- Related: `skills/devian-unity/20-packages/com.devian.unity/SKILL.md`
- Related: `skills/devian-unity/30-unity-components/00-unity-object-destruction/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
