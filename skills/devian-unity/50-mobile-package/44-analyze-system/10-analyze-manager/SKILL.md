# 10-analyze-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `AnalyzeManager` 설계 문서다.

---

## Implementation Location (3-path mirror)

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `AnalyzeManager.cs` | `upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Analyze/` | `Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Analyze/` | `Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Analyze/` |

---

## Public API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `LogEvent(string eventName)`
- `LogEvent(string eventName, string paramName, string paramValue)`
- `LogEvent(string eventName, string paramName, long paramValue)`
- `LogEvent(string eventName, params Parameter[] parameters)`

---

## Internal Responsibilities

- Firebase Analytics 초기화 상태 관리
- `FirebaseAnalytics.LogEvent` 래핑
- 초기화 전 호출은 silent drop (`Debug.LogWarning`)

---

## Hard Rules

- 플랫폼 adapter 없음 (Firebase SDK가 iOS/Android 공통)
- 테이블/Storage/MessageTrigger 없음
- `LogEvent`는 동기(fire-and-forget). async 불필요
- 초기화 전 호출은 에러가 아닌 silent drop

---

## Related

- [01-policy](../01-policy/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
