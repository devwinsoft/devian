# 44-analyze-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

분석 시스템(`AnalyzeManager`)의 공개 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) AnalyzeManager는 Firebase Analytics의 래퍼다

- 직접 집계/저장하지 않는다.
- Firebase SDK의 `FirebaseAnalytics.LogEvent`를 호출하는 것이 유일한 책임이다.

### 2) 이벤트 이름은 Firebase 제약을 준수한다

- 영문자로 시작해야 한다.
- 영숫자 + 밑줄(`_`)만 허용한다.
- 최대 40자다.

### 3) Firebase 이벤트 제한을 준수한다

- 앱당 이벤트 타입 최대 500종.
- LogEvent 호출당 파라미터 최대 25개.

### 4) 초기화 전 호출은 silent drop이다

- `IsInitialized == false` 상태에서 `LogEvent` 호출 시 에러를 반환하지 않는다.
- `Debug.LogWarning`으로 경고만 출력하고 무시한다.

### 5) 테이블/Storage는 없다

- TB_ANALYZE 테이블 없음.
- AnalyzeStorage 없음.
- fire-and-forget 방식으로 Firebase Analytics에 전송만 한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-analyze-manager](../10-analyze-manager/SKILL.md)
