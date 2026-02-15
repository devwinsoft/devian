# 15-common-error

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **CommonError / CommonErrorType / ERROR_COMMON 테이블 규약**의 정본이다.

---

## 목적/범위

Common 레이어의 에러 표현을 표준화한다.

- 런타임 에러 객체: `CommonError`
- 에러 코드 enum: `CommonErrorType`
- 에러 코드 마스터: `CommonTable.xlsx`의 `ERROR_COMMON` 시트

---

## Canonical Source (Hard Rule)

### ERROR_COMMON (XLSX)

`CommonErrorType`의 정본은 아래 테이블이다.

- 파일: `input/Domains/Common/tables/CommonTable.xlsx`
- 시트: `ERROR_COMMON`
- 컬럼(최소): `id`, `msg_key`, `msg`

Hard Rule:
- `ERROR_COMMON`는 **append(맨 아래 행 추가)만 허용**한다.
- 중간 삽입/정렬/행 재배치 금지 (기존 enum 값이 변동될 수 있음)
- 새 코드가 필요하면 `ERROR_COMMON`에 추가한 뒤 **생성 파이프라인을 실행**해 `CommonErrorType`을 갱신한다.

---

## Runtime Type: CommonError

### 정의

- `CommonError.Code`의 타입은 **반드시 `CommonErrorType`** 이다.
- 문자열 코드(string) 기반 에러 코드는 정식 경로로 사용하지 않는다.

필드(개념):
- `Code: CommonErrorType`
- `Message: string`
- `Details: string?`

### 생성 규칙

- 정상 경로: `new CommonError(CommonErrorType.X, message, details?)`
- 레거시 문자열 코드가 들어오는 경로가 남아있다면:
  - `CommonErrorType.COMMON_UNKNOWN`으로 매핑하고
  - 레거시 코드는 `Details`에 `legacyCode=...` 형태로 보존한다.

---

## 소비 규칙

- 비교는 enum 비교로만 한다.
  - `err.Code == CommonErrorType.X`
- 외부 출력/로그에서 문자열이 필요하면:
  - `err.Code.ToString()`을 사용한다.

---

## DoD

Hard
- `ERROR_COMMON`에 새 항목 추가 시 **append만** 수행되었음
- 런타임 에러의 `Code`는 `CommonErrorType`으로 유지됨

Soft
- 없음
