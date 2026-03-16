# 03-ssot — 44-analyze-system

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Analyze 시스템의 정본이다.

- 테이블: 없음
- Storage: 없음
- 의존: `Firebase.Analytics` SDK
- API 정본: `AnalyzeManager.cs`

---

## A. 의존 SDK

- `Firebase.Analytics.FirebaseAnalytics.LogEvent` 계열 API를 사용한다.
- Firebase Analytics는 Firebase 초기화(`FirebaseApp.DefaultInstance`) 시 자동 활성화된다.
- `Firebase.Analytics.dll`은 프로젝트에 이미 포함되어 있다.

---

## B. 제한 사항 (Firebase)

| 항목 | 제한 |
|------|------|
| 이벤트 타입 | 앱당 최대 500종 |
| 이벤트 발생량 | 무제한 |
| 이벤트당 파라미터 | 최대 25개 / 호출 |
| 이벤트 이름 | 영문자 시작, 영숫자+밑줄, 최대 40자 |

---

## Related

- [10-analyze-manager](../10-analyze-manager/SKILL.md)
