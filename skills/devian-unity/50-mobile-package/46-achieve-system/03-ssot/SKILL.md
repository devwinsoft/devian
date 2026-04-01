# 03-ssot — 46-achieve-system

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Achieve 시스템의 정본이다.

- 업적 runtime 테이블: `ACHIEVE_SOCIAL`, `ACHIEVE_PASS`
- 진행 stat 정본: `MESSAGE`
- 업적 알림 enum: `ACHIEVE_MESSAGE_TYPE`
- 업적 타입 enum: `ACHIEVE_TYPE` (`NONE`, `SOCIAL`, `PASS`)
- 플랫폼 업적 매핑: `ACHIEVE_SOCIAL.achieve_id -> (apple_achievement_id, google_achievement_id)`
- 저장 구조: `AchieveStorage` + `AchieveRuntimeBase`

---

## A. Runtime Source

- 파일: `input/Domains/Game/AchieveTable.xlsx`
- 시트: `ACHIEVE_SOCIAL`
- 컨테이너: `TB_ACHIEVE_SOCIAL`

### `ACHIEVE_SOCIAL` schema

| field | type | note |
|------|------|------|
| `index` | int (pk) | row pk |
| `achieve_id` | string | 업적 그룹/내부 업적 ID |
| `is_active` | bool | 운영 토글 |
| `level` | int | 단계 |
| `order_num` | int | 정렬 기준(1-base) |
| `req_msg_id` | string | runtime 활성화 조건 메시지 (`MESSAGE.message_id` FK) |
| `req_value` | `class:CBigInt` | runtime 활성화 조건값 |
| `condition_msg_id` | string | 진행도 계산 메시지 (`MESSAGE.message_id` FK) |
| `condition_value` | `class:CBigInt` | 목표값 |
| `reward_group_id` | string | claim 보상 키 |
| `apple_achievement_id` | string | Game Center ID (`achieve_id` group 공통) |
| `google_achievement_id` | string | GPGS ID (`achieve_id` group 공통) |

### `ACHIEVE_PASS` schema

- 파일: `input/Domains/Game/AchieveTable.xlsx`
- 시트: `ACHIEVE_PASS`
- 컨테이너: `TB_ACHIEVE_PASS`

| field | type | note |
|------|------|------|
| `index` | int (pk) | row pk |
| `achieve_id` | string | 업적 그룹/내부 업적 ID |
| `is_active` | bool | 운영 토글 |
| `level` | int | 단계 |
| `order_num` | int | 정렬 기준(1-base) |
| `req_pass_id` | string | runtime 활성화 조건 pass ID (`InventoryStorage.Passes` key) |
| `req_season_id` | string | runtime 활성화 조건 season ID (`SEASON.season_id` FK, 시즌 기간 조건) |
| `condition_msg_id` | string | 선택 조건 메시지 (`MESSAGE.message_id` FK), 비어있으면 즉시 claim 경로 |
| `condition_value` | `class:CBigInt` | 선택 목표값 (`condition_msg_id`가 있을 때 사용) |
| `reward_group_id` | string | claim 보상 키 |

---

## B. Runtime/Storage Rules

`AchieveStorage`:
- `schemaVersion`
- `nextAchieveUid`
- `runtimes: Dictionary<int, AchieveRuntimeBase>`

`AchieveRuntimeBase` 저장 필드:
- `achieveType`, `achieve_id`, `achieveUid`, `level`, `index`, `progressValue`, `state`

규칙:
- period 개념 없음 (`periodKey` 없음)
- group(`achieve_id`)당 runtime 1개 유지
- level-up 시 `achieveUid` 유지
- 초기화 시 `achieve_id` group 기준 runtime을 항상 생성한다.
- 타입별 req 규칙으로 `WAIT/ACTIVE` 시작 상태를 결정한다.
  - `ACHIEVE_SOCIAL`: `req_msg_id/req_value`
  - `ACHIEVE_PASS`: `req_pass_id` / `req_season_id`
- `WAIT` 상태는 `condition_msg_id` 진행도 반영을 하지 않는다.
- req 조건을 만족하면 `WAIT -> ACTIVE`로 전이한다.
  - req message 조건: `GameMessageManager` stat/trigger 기반
  - req pass 조건: `InventoryManager.Instance.Storage.Passes` 보유 체크
- 진행값은 saveType 규칙을 따른다:
  - `TOTAL_SUM`, `TOTAL_MAX`: 외부 저장(`GameMessageStorage`) 값을 projection
  - `SESSION_SUM`, `SESSION_MAX`: runtime 내부 `progressValue`를 직접 누적/갱신

---

## C. Claim / Level-up Rules

claim 성공 시:
1. current row reward 적용
2. next level row가 있으면 runtime 재바인딩
   - next row의 타입별 req 조건을 다시 평가해 `WAIT` 또는 `ACTIVE`로 전환
3. next가 없으면 completed
4. 저장 수행
5. 플랫폼 unlock best-effort (`ACHIEVE_TYPE.SOCIAL`만 수행)

---

## D. Event

- `OnRuntimeInitialized(AchieveRuntimeBase)`
- `OnRuntimeActive(AchieveRuntimeBase)` (`WAIT -> ACTIVE` 전이 시 1회)
- `OnRuntimeProgress(AchieveRuntimeBase)`
- `OnRuntimeClaimable(AchieveRuntimeBase)`
- `OnRuntimeLevelUp(AchieveRuntimeBase)`
- `OnRuntimeRewarded(AchieveRuntimeBase, RewardData[])`
- `OnAchievementUnlocked(string achievementId)`

`AchieveMessageTrigger` payload 규약:
- key: `ACHIEVE_MESSAGE_TYPE`
- `RUNTIME_*`: `args[0] = AchieveRuntimeBase`
- `RUNTIME_REWARDED`: `args[1] = RewardData[]`
- `RUNTIME_UNLOCKED`: `args[0] = string achievementId`

---

## Related

- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
