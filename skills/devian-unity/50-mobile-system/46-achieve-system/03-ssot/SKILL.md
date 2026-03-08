# 03-ssot — 46-achieve-system

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Achieve 시스템의 정본이다.

- 업적 runtime 테이블: `ACHIEVE`
- 진행 stat 정본: `MESSAGE`
- 업적 알림 enum: `ACHIEVE_MESSAGE`
- 업적 타입 enum: `ACHIEVE_TYPE` (`ENUM_MISSION.json`)
- 플랫폼 업적 매핑: `ACHIEVE.achieveId -> (appleAchievementId, googleAchievementId)`
- 저장 구조: `AchieveStorage` + `AchieveRuntime`

---

## A. Runtime Source

- 파일: `input/Domains/Game/GameTable.xlsx`
- 시트: `ACHIEVE`
- 컨테이너: `TB_ACHIEVE`

### `ACHIEVE` schema

| field | type | note |
|------|------|------|
| `index` | int (pk) | row pk |
| `achieveId` | string | 업적 그룹/내부 업적 ID |
| `achieveType` | `ACHIEVE_TYPE` | 업적 타입 |
| `isActive` | bool | 운영 토글 |
| `level` | int | 단계 |
| `orderNum` | int | 정렬 기준(1-base) |
| `reqMsgId` | string | runtime 활성화 조건 메시지 (`MESSAGE.messageId` FK) |
| `reqPassId` | string | runtime 활성화 조건 시즌패스 ID (`InventoryStorage.Passes` key) |
| `reqValue` | `class:CBigInt` | runtime 활성화 조건값 |
| `conditionMsgId` | string | 진행도 계산 메시지 (`MESSAGE.messageId` FK) |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | claim 보상 키 |
| `appleAchievementId` | string | Game Center ID (`achieveId` group 공통) |
| `googleAchievementId` | string | GPGS ID (`achieveId` group 공통) |

---

## B. Runtime/Storage Rules

`AchieveStorage`:
- `schemaVersion`
- `nextAchieveUid`
- `runtimes: Dictionary<int, AchieveRuntime>`

`AchieveRuntime` 저장 필드:
- `achieveId`, `messageId`, `achieveUid`, `level`, `progressValue`, `isWaiting`, `isCompleted`
  - `messageId`는 현재 level의 `conditionMsgId`를 저장한다.

규칙:
- period 개념 없음 (`periodKey` 없음)
- group(`achieveId`)당 runtime 1개 유지
- level-up 시 `achieveUid` 유지
- 초기화 시 `ACHIEVE` group 기준 runtime을 항상 생성한다.
- `reqMsgId/reqValue` 또는 `reqPassId`가 있으면 `WAIT`, 둘 다 없으면 `ACTIVE`로 시작한다.
- `WAIT` 상태는 `conditionMsgId` 진행도 반영을 하지 않는다.
- req 조건을 만족하면 `WAIT -> ACTIVE`로 전이한다.
  - `reqMsgId/reqValue` 조건: 기존 규칙(`GameMessageManager` stat/trigger 기반)
  - `reqPassId` 조건: `InventoryManager.Instance.Storage.Passes`에 해당 pass가 `owned=true`
  - 둘 다 존재하면 AND 조건으로 평가한다.
- 진행값은 saveType 규칙을 따른다:
  - `TOTAL_SUM`, `TOTAL_MAX`: 외부 저장(`GameMessageStorage`) 값을 projection
  - `SESSION_SUM`, `SESSION_MAX`: runtime 내부 `progressValue`를 직접 누적/갱신

---

## C. Claim / Level-up Rules

claim 성공 시:
1. current row reward 적용
2. next level row가 있으면 runtime 재바인딩
   - next row의 `reqMsgId/reqValue`를 다시 평가해 `WAIT` 또는 `ACTIVE`로 전환
3. next가 없으면 completed
4. 저장 수행
5. 플랫폼 unlock best-effort

---

## D. Event

- `OnRuntimeInitialized(AchieveRuntime)`
- `OnRuntimeActive(AchieveRuntime)` (`WAIT -> ACTIVE` 전이 시 1회)
- `OnRuntimeProgress(AchieveRuntime)`
- `OnRuntimeClaimable(AchieveRuntime)`
- `OnRuntimeLevelUp(AchieveRuntime)`
- `OnRuntimeRewarded(AchieveRuntime, RewardData[])`
- `OnAchievementUnlocked(string achievementId)`

`AchieveMessageTrigger` payload 규약:
- key: `ACHIEVE_MESSAGE`
- `RUNTIME_*`: `args[0] = AchieveRuntime`
- `RUNTIME_REWARDED`: `args[1] = RewardData[]`
- `RUNTIME_UNLOCKED`: `args[0] = string achievementId`

---

## Related

- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
