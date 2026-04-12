# 14-game-error

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **GameError / GAME_ERROR_TYPE / GAME_ERROR 테이블 규약**의 정본이다.

---

## 목적/범위

Game 도메인 내부의 에러 표현을 표준화한다.

- 런타임 에러 객체: `GameError`
- 에러 코드 enum: `GAME_ERROR_TYPE`
- 에러 코드 마스터: `GameErrorTable.xlsx`의 `GAME_ERROR` 시트
- 주 용도: Game 도메인 public API 실패 식별자 (table lookup, ability/stat/reward, MobilePackage Game managers)

Boundary Rule:
- 저장/네트워크/인프라/공통 입력 검증 실패는 [20-domain-common/16-common-error](../../20-domain-common/16-common-error/SKILL.md)의 `CommonError`를 사용한다.
- Game 테이블 조회, Ability factory validation/projection, Game 도메인 규칙 실패는 `GameError`를 사용한다.

---

## Canonical Source (Hard Rule)

### GAME_ERROR (XLSX)

`GAME_ERROR_TYPE`의 정본은 아래 테이블이다.

- 파일: `input/Domains/Game/GameErrorTable.xlsx`
- 시트: `GAME_ERROR`
- 컬럼(최소): `id`, `msg_key`, `msg`

Hard Rule:
- `GAME_ERROR`는 **append(맨 아래 행 추가)만 허용**한다.
- 중간 삽입/정렬/행 재배치 금지 (기존 enum 값이 변동될 수 있음)
- 새 코드가 필요하면 `GAME_ERROR`에 추가한 뒤 생성 파이프라인을 실행해 `GAME_ERROR_TYPE`을 갱신한다.

### Prefix Taxonomy (Hard)

- `GAME_*` — Game 도메인 공통 validation/unknown
- `ABILITY_*` — Ability factory/projection/table lookup
- `REWARD_*` — reward table lookup/apply 규칙 실패
- `INVENTORY_*` — inventory apply/revoke 수량 검증
- `INVENTORY_CARD_LEVELUP_COUNT_INSUFFICIENT` — card level-up 비용(`ITEM_CARD_LEVEL.levelup_count`) 부족
- `INVENTORY_HERO_LEVELUP_COUNT_INSUFFICIENT` — hero level-up 비용(`ITEM_HERO_LEVEL.levelup_count`) 부족
- `INVENTORY_EQUIP_LEVELUP_MATERIAL_INSUFFICIENT` — equip level-up 재료 비용(`ITEM_EQUIP_LEVEL.levelup_material`, `levelup_count`) 부족
- `INVENTORY_ITEM_LEVELUP_CURRENCY_INSUFFICIENT` — item level-up 재화 비용(`ITEM_*_LEVEL.levelup_currency`, `levelup_price`) 부족
- `MISSION_*` — mission claim/initialize 규칙 실패
- `ACHIEVE_*` — achieve claim 규칙 실패
- `SHOP_*` — shop 구매/제한/갱신/적용 실패
- `TREASURE_*` — treasure chest/reward 실패
- `LEADERBOARD_*` — leaderboard score/season/platform 실패
- `PURCHASE_*` — IAP 구매 검증/환불/entitlements/서버 호출 실패
- `ATTEND_*` — 출석 claim/initialize 규칙 실패
- `ADS_*` — 광고 load/show/placement 실패
- `IAP_*` — IAP 스토어 초기화/제품 조회 실패

### Numeric Value Rule (Hard)

- `GAME_ERROR_TYPE.SUCCESS`는 값이 **반드시 `0`** 이어야 한다.
- 실패 코드는 모두 `> 0` 이어야 한다.
- `GAME_ERROR` 첫 항목은 `SUCCESS`여야 한다.
- 정상 경로는 `GameResult`의 성공 상태(`Error == null`)로 표현하고, 실패에 `SUCCESS`를 쓰지 않는다.

---

## Runtime Type: GameError

필드:
- `Code: GAME_ERROR_TYPE`
- `Message: string`
- `Details: string?`

생성 규칙:
- `new GameError(GAME_ERROR_TYPE.X, message, details?)`
- 문자열 코드(string) 기반 GameError 생성 경로는 두지 않는다.

소비 규칙:
- 비교는 `err.Code == GAME_ERROR_TYPE.X`
- 로그 문자열은 `err.Code.ToString()` 사용

---

## DoD

Hard
- `GAME_ERROR`에 새 항목 추가 시 append만 수행되었음
- `GameError.Code`는 `GAME_ERROR_TYPE`으로 유지됨
- `SUCCESS == 0`, 실패 코드는 `> 0` 유지됨
- 공통 인프라 실패를 `GameError`로 끌어올리지 않았음

---
