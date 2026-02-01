# 10-unity-main-thread

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Unity 메인스레드 강제 유틸(`UnityMainThread`, `UnityMainThreadDispatcher`)의 규약을 정의한다.

---

## 목표

- Unity API 호출이 반드시 메인 스레드에서만 수행되도록 보장하는 유틸리티 제공
- 백그라운드 스레드에서 메인 스레드로 작업을 디스패치하는 큐 시스템 제공

---

## 파일 위치 및 소유권

### 소스 파일

| 파일 | 설명 |
|------|------|
| `com.devian.foundation/Runtime/Unity/_Shared/UnityMainThread.cs` | 메인 스레드 감지 헬퍼 |
| `com.devian.foundation/Runtime/Unity/_Shared/UnityMainThreadDispatcher.cs` | 백그라운드→메인 스레드 디스패처 |

### 소유권 정책 (Hard Rule)

**`Runtime/_Shared`는 고정 유틸(수기 코드) 영역이다.**

| 항목 | 정책 |
|------|------|
| 정본 | `framework-cs/upm/com.devian.foundation/Runtime/Unity/_Shared/` |
| 복사본 | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Unity/_Shared/` |
| 생성기 | `Runtime/Generated/**`만 다룸 — `_Shared`는 건드리지 않음 |

> **Note:** 생성기는 `Runtime/_Shared`를 clean/generate 하지 않는다. 이 파일들은 수기 유지되며, 빌더가 upm → Packages로 패키지 레벨 복사(sync)를 수행한다.
>
> **Reference:** `skills/devian-unity/03-ssot/SKILL.md`

---

## 규약

### UnityMainThread

- `internal static class`
- **초기화 타이밍 (Hard Rule):**
  - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`로 메인 스레드 ID 캡처
  - SubsystemRegistration은 BeforeSceneLoad보다 **더 이른 시점**에 실행됨
  - 초기값: `s_mainThreadId = 0` (미초기화 상태)
  
- **메서드:**
  - `InitIfNeeded()`: 미초기화 상태(`s_mainThreadId == 0`)면 현재 스레드 ID로 초기화. 여러 번 호출해도 안전.
  - `IsMainThread` 속성: 현재 스레드가 메인 스레드인지 반환. 자동으로 `InitIfNeeded()` 호출.
  - `EnsureOrThrow(string context)`: 메인 스레드가 아니면 예외 발생. 자동으로 `InitIfNeeded()` 호출.

- **초기화 전 EnsureOrThrow 호출 (Hard Rule):**
  - 과거: 초기화 전에 `EnsureOrThrow`가 호출되면 false negative 발생 (메인 스레드인데도 throw)
  - 현재: `EnsureOrThrow` 내부에서 `InitIfNeeded()`를 먼저 호출하여 이 문제 방지

```csharp
// 현재 구현
public static void EnsureOrThrow(string context)
{
    InitIfNeeded();  // 미초기화면 현재 스레드로 초기화
    
    if (s_mainThreadId != Thread.CurrentThread.ManagedThreadId)
    {
        throw new InvalidOperationException(...);
    }
}
```

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

## DoD (완료 정의) — Hard Gate

- [ ] `com.devian.foundation/Runtime/Unity/_Shared/UnityMainThread.cs` 존재
- [ ] `com.devian.foundation/Runtime/Unity/_Shared/UnityMainThreadDispatcher.cs` 존재
- [ ] UPM 경로와 UnityExample/Packages 경로의 파일 내용이 동일함
- [ ] Dispatcher는 `maxPerFrame` 제한(500)을 가짐
- [ ] Unity API 호출(`Debug.Log*`)은 메인 스레드에서만 수행됨
- [ ] UnityMainThread 캡처 타이밍이 `SubsystemRegistration`임
- [ ] `EnsureOrThrow`가 `InitIfNeeded()`를 먼저 호출하여 초기화 전 오판 방지

**FAIL 조건:**

- `_Shared` 파일이 UPM과 Packages에서 불일치
- `UnityMainThreadDispatcher`가 `_Shared` 밖에 위치함
- 초기화 전 `EnsureOrThrow` 호출 시 false negative 발생

---

## 금지

- `_Shared` 파일을 Packages에서 직접 수정 금지 (정본은 upm)
- Dispatcher를 별도 패키지로 분리 금지
- `UnityMainThread` 또는 `UnityMainThreadDispatcher`를 `_Shared` 밖으로 이동 금지

---

## Reference

- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/30-unity-components/00-unity-object-destruction/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
- Related: `skills/devian-unity/03-ssot/SKILL.md` (소유권 정책)
