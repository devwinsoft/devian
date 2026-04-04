# 15-game-result

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **GameResult / GameResult<T>** 규약의 정본이다.

---

## 목적/범위

Game 도메인 public API에서 "성공/실패 + GameError"를 표현하는 표준 Result 컨테이너를 고정한다.

- 결과 타입: `GameResult`, `GameResult<T>`
- 에러 타입: `GameError`
- 주 용도: Ability factory, Game table lookup wrapper, reward/stat domain API, MobilePackage Game managers (Inventory, Mission, Shop, Purchase, Leaderboard, Attend, Ads 등)의 recoverable failure 전달

Common 경계:
- 저장/네트워크/공통 config/공통 입력 boundary는 [20-domain-common/17-common-result](../../20-domain-common/17-common-result/SKILL.md)의 `CommonResult`를 유지한다.
- Game 도메인 규칙 실패를 돌려주는 API는 `GameResult`를 사용한다.
- Boundary adapter: Game manager가 CommonResult를 반환하는 boundary API를 호출할 때 `ToGameResult(CommonResult)` 헬퍼로 변환하고, 반대 방향(SaveData/Login 등)은 `ToCommonResult(GameResult)` 헬퍼로 변환한다.

---

## Runtime Type

### GameResult

- `Error: GameError?`
- `IsSuccess / IsFailure`
- 성공 생성: `GameResult.Ok()`
- 실패 생성: `GameResult.Failure(GameError error)`, `GameResult.Failure(GAME_ERROR_TYPE errorType, string message, string? details = null)`

### GameResult<T>

- `Value: T?`
- `Error: GameError?`
- `IsSuccess / IsFailure`
- 성공 생성: `GameResult<T>.Success(value)`
- 실패 생성: `GameResult<T>.Failure(GameError error)`, `GameResult<T>.Failure(GAME_ERROR_TYPE errorType, string message, string? details = null)`

Hard Rule:
- 실패 생성 시 `GameError.Code`는 반드시 `GAME_ERROR_TYPE` 기반이어야 한다.
- `GAME_ERROR_TYPE.SUCCESS`를 실패 코드로 사용하지 않는다.
- Game domain public API의 예상 가능한 validation/lookup/projection 실패는 `throw` 대신 `GameResult.Failure(...)`를 우선한다.

---

## 소비 규칙

- 호출부는 `IsFailure`/`IsSuccess`로 분기한다.
- 실패 시 `Error`는 null이 아니어야 한다.
- 성공 시 `Error`는 null이어야 한다.
- Game API의 정상 경로를 `GAME_ERROR_TYPE.SUCCESS`로 표현하지 않는다.

---

## DoD

Hard
- Game 도메인 public API 실패가 `GameResult` + `GameError(GAME_ERROR_TYPE, ...)`로 표현됨
- 정상 경로는 `Error == null`로 표현됨
- infra/common boundary failure를 `GameResult`로 억지 변환하지 않았음

---
