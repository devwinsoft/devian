# 17-common-result

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **CommonResult** 규약의 정본이다.

---

## 목적/범위

Common 레이어에서 "성공/실패 + 에러"를 표현하는 표준 Result 컨테이너를 고정한다.

- 결과 타입: `CommonResult<T>`
- 에러 타입: `CommonError`
- 주 용도: public/boundary API의 recoverable failure 전달

경계 분리:
- Game table lookup / Ability factory / Game 도메인 규칙 실패는 [21-domain-game/15-game-result](../../21-domain-game/15-game-result/SKILL.md)의 `GameResult`를 사용한다.

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
  - `CommonResult<T>.Failure(COMMON_ERROR_TYPE errorType, string message, string? details = null)`

Hard Rule:
- 실패 생성 시 `CommonError.Code`는 반드시 `COMMON_ERROR_TYPE` 기반이어야 한다.
- string 기반 error code는 정식 경로로 사용하지 않는다.
- `COMMON_ERROR_TYPE.SUCCESS`(reserved, `0`)를 실패 코드로 사용하지 않는다.
- 예상 가능한 validation / lookup / restore 실패는 `throw`보다 `CommonResult.Failure(...)`를 우선한다.

Boundary-First Rule:
- public API, data boundary, config parse, save/load, Common table lookup은 `CommonResult`를 우선한다.
- private/internal helper는 이미 검증된 내부 invariant가 깨질 때만 예외를 허용할 수 있다.
- helper 예외를 이유 없이 public API의 기본 실패 모델로 승격시키지 않는다.

대표 경계:
- Common config parse/lookup
- save payload deserialize
- first-init config parse
- SaveData codec (GameResult → CommonResult 변환: `ToCommonResult` 헬퍼)
- LoginManager (Game manager 호출 결과를 CommonResult로 변환하여 전달)

레거시(string code) Failure 오버로드가 남아있다면:
- `COMMON_ERROR_TYPE.COMMON_UNKNOWN`으로 매핑하고
- `Details`에 `legacyCode=...`를 보존한다.

---

## 소비 규칙

- 호출부는 `IsFailure`/`IsSuccess`로 분기한다.
- 실패 시 `Error`는 null이 아니어야 한다(불변식 유지).
- 성공 시 `Error`는 null이어야 하며, 성공을 `COMMON_ERROR_TYPE.SUCCESS`로 대체 표현하지 않는다.
- 외부 응답/프로토콜에서 numeric code `0`(success)를 받는 경우:
  - `CommonError`를 만들지 말고 `CommonResult` 성공으로 매핑한다.
- recover 가능한 경계 실패를 `catch` 없이 예외 전파에 의존하지 않는다.

---

## DoD

Hard
- `CommonResult<T>` 실패는 `CommonError(COMMON_ERROR_TYPE, ...)` 기반으로 표현됨
- 성공은 `Error == null`로 표현되며 `COMMON_ERROR_TYPE.SUCCESS`를 `Failure(...)`에 사용하지 않음
- public/boundary failure 모델이 `throw` 대신 `CommonResult`로 유지됨

Soft
- 없음
