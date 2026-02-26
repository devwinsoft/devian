# 16-common-result

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **CommonResult** 규약의 정본이다.

---

## 목적/범위

Common 레이어에서 "성공/실패 + 에러"를 표현하는 표준 Result 컨테이너를 고정한다.

- 결과 타입: `CommonResult<T>`
- 에러 타입: `CommonError`

---

## Runtime Type: CommonResult<T>

### 정의

`CommonResult<T>`는 다음을 포함한다.

- `Value: T?`
- `Error: CommonError?`
- `IsSuccess / IsFailure` 등의 판정 프로퍼티

### 생성 규칙(개념)

- 성공:
  - `CommonResult<T>.Success(value)`
- 실패:
  - `CommonResult<T>.Failure(CommonError error)`
  - `CommonResult<T>.Failure(CommonErrorType errorType, string message, string? details = null)`

Hard Rule:
- 실패 생성 시 `CommonError.Code`는 반드시 `CommonErrorType` 기반이어야 한다.
- string 기반 error code는 정식 경로로 사용하지 않는다.
- `CommonErrorType.SUCCESS`(reserved, `0`)를 실패 코드로 사용하지 않는다.

레거시(string code) Failure 오버로드가 남아있다면:
- `CommonErrorType.COMMON_UNKNOWN`으로 매핑하고
- `Details`에 `legacyCode=...`를 보존한다.

---

## 소비 규칙

- 호출부는 `IsFailure`/`IsSuccess`로 분기한다.
- 실패 시 `Error`는 null이 아니어야 한다(불변식 유지).
- 성공 시 `Error`는 null이어야 하며, 성공을 `CommonErrorType.SUCCESS`로 대체 표현하지 않는다.
- 외부 응답/프로토콜에서 numeric code `0`(success)를 받는 경우:
  - `CommonError`를 만들지 말고 `CommonResult` 성공으로 매핑한다.

---

## DoD

Hard
- `CommonResult<T>` 실패는 `CommonError(CommonErrorType, ...)` 기반으로 표현됨
- 성공은 `Error == null`로 표현되며 `CommonErrorType.SUCCESS`를 `Failure(...)`에 사용하지 않음

Soft
- 없음
