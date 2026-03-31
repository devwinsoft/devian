# 16-common-error

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **CommonError / COMMON_ERROR_TYPE / COMMON_ERROR 테이블 규약**의 정본이다.

---

## 목적/범위

Common 레이어의 에러 표현을 표준화한다.

- 런타임 에러 객체: `CommonError`
- 에러 코드 enum: `COMMON_ERROR_TYPE`
- 에러 코드 마스터: `CommonTable.xlsx`의 `COMMON_ERROR` 시트
- 주 용도: public/boundary API가 호출자에게 돌려줄 실패 식별자

중요:
- `COMMON_ERROR`는 boundary failure 식별 체계다.
- private helper 내부 invariant 예외를 모두 `COMMON_ERROR_TYPE`로 승격시키는 용도로 쓰지 않는다.

---

## Canonical Source (Hard Rule)

### COMMON_ERROR (XLSX)

`COMMON_ERROR_TYPE`의 정본은 아래 테이블이다.

- 파일: `input/Domains/Common/CommonTable.xlsx`
- 시트: `COMMON_ERROR`
- 컬럼(최소): `id`, `msg_key`, `msg`

Hard Rule:
- `COMMON_ERROR`는 **append(맨 아래 행 추가)만 허용**한다.
- 중간 삽입/정렬/행 재배치 금지 (기존 enum 값이 변동될 수 있음)
- 새 코드가 필요하면 `COMMON_ERROR`에 추가한 뒤 **생성 파이프라인을 실행**해 `COMMON_ERROR_TYPE`을 갱신한다.
- 예: ability factory lookup 실패는 `ABILITY_ITEM_TABLE_NOT_FOUND`, `ABILITY_UNIT_TABLE_NOT_FOUND`처럼 전용 코드를 추가해 구분한다.

### Prefix Taxonomy (Hard)

새 코드는 prefix 기준으로 분류한다.

- `COMMON_*` — 범용 인프라/공통 입력/네트워크/서버/unknown
- `ABILITY_*` — ability factory/projection/table lookup
- `INVENTORY_*` — inventory delta 검증/회수/수량 부족
- `SHOP_*` — shop 구매/제한/광고/적용 실패
- `TREASURE_*` — treasure chest/reward 실패
- `PUSH_*` — push 알림 관련 실패
- `VERSION_CHECK_*` — 버전 체크 관련 실패

### When To Add A New Code

새 `COMMON_ERROR_TYPE`은 아래 조건을 만족할 때만 추가한다.

- 호출자가 코드별 분기를 해야 한다.
- 운영/로그 분석에서 독립 식별자가 필요하다.
- `COMMON_INVALID_ARGUMENT` 또는 `COMMON_UNKNOWN`으로는 의미가 흐려진다.

추가하지 않는 경우:

- private helper 내부 예외
- 호출자가 동일하게 처리할 실패
- 메시지만 다르고 정책은 같은 실패

### Numeric Value Rule (Hard)

- `COMMON_ERROR_TYPE.SUCCESS`는 **예약된 센티넬 코드**이며, 값은 **반드시 `0`** 이어야 한다.
- `COMMON_ERROR_TYPE`의 **실패 코드들은 모두 `> 0`** 이어야 한다.
- `COMMON_ERROR`의 첫 항목(값 `0`)은 `SUCCESS`여야 한다. (예: `id=SUCCESS`, `msg=Success`)
- `SUCCESS`는 주로 기본값/외부 연동/테이블 정합성 용도이며, 정상 경로 자체는 `CommonResult`의 성공 상태(`Error == null`)로 표현한다.
- 초기 정렬(부트스트랩) 이후에는 append-only 규칙을 계속 유지한다. (`SUCCESS=0` 고정, 실패 코드는 뒤에만 추가)

---

## Runtime Type: CommonError

### 정의

- `CommonError.Code`의 타입은 **반드시 `COMMON_ERROR_TYPE`** 이다.
- 문자열 코드(string) 기반 에러 코드는 정식 경로로 사용하지 않는다.

필드(개념):
- `Code: COMMON_ERROR_TYPE`
- `Message: string`
- `Details: string?`

### 생성 규칙

- 정상 경로: `new CommonError(COMMON_ERROR_TYPE.X, message, details?)`
- 금지: `new CommonError(COMMON_ERROR_TYPE.SUCCESS, ...)` (정상 경로는 `CommonResult` 성공으로 표현)
- 레거시 문자열 코드가 들어오는 경로가 남아있다면:
  - `COMMON_ERROR_TYPE.COMMON_UNKNOWN`으로 매핑하고
  - 레거시 코드는 `Details`에 `legacyCode=...` 형태로 보존한다.

Boundary Rule:
- validation / lookup / restore / config parse 같은 public 경계 실패는 `CommonError`로 표준화한다.
- internal invariant 위반은 `CommonError`를 새로 추가하기보다 예외로 남길 수 있다.

---

## 소비 규칙

- 비교는 enum 비교로만 한다.
  - `err.Code == COMMON_ERROR_TYPE.X`
- 외부 출력/로그에서 문자열이 필요하면:
  - `err.Code.ToString()`을 사용한다.

---

## DoD

Hard
- `COMMON_ERROR`에 새 항목 추가 시 **append만** 수행되었음
- 런타임 에러의 `Code`는 `COMMON_ERROR_TYPE`으로 유지됨
- `COMMON_ERROR_TYPE.SUCCESS == 0` 이고, 실패 코드는 `> 0` 임
- 새 코드가 taxonomy/policy 기준에 맞게 추가되었음

Soft
- 없음
