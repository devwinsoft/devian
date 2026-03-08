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
| `achieveType` | `ACHIEVE_TYPE` | 업적 타입 (`DEFAULT`는 초기화 시 runtime 자동 생성) |
| `isActive` | bool | 운영 토글 |
| `level` | int | 단계 |
| `orderNum` | int | 정렬 기준(1-base) |
| `messageId` | string | `MESSAGE.messageId` FK |
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
- `achieveId`, `messageId`, `achieveUid`, `level`, `progressValue`, `isCompleted`

규칙:
- period 개념 없음 (`periodKey` 없음)
- group(`achieveId`)당 runtime 1개 유지
- level-up 시 `achieveUid` 유지
- 초기화 시 자동 runtime 생성은 `achieveType == DEFAULT`인 level=1 row만 대상
- 진행값은 saveType 규칙을 따른다:
  - `TOTAL_SUM`, `TOTAL_MAX`: 외부 저장(`GameMessageStorage`) 값을 projection
  - `SESSION_SUM`, `SESSION_MAX`: runtime 내부 `progressValue`를 직접 누적/갱신

---

## C. Claim / Level-up Rules

claim 성공 시:
1. current row reward 적용
2. next level row가 있으면 runtime 재바인딩
3. next가 없으면 completed
4. 저장 수행
5. 플랫폼 unlock best-effort

---

## D. Event

- `OnRuntimeInitialized(AchieveRuntime)`
- `OnRuntimeProgress(AchieveRuntime)`
- `OnRuntimeClaimable(AchieveRuntime)`
- `OnRuntimeLevelUp(AchieveRuntime)`
- `OnRuntimeRewarded(AchieveRuntime, RewardData[])`
- `OnAchievementUnlocked(string achievementId)`

`AchieveMessageTrigger` payload 규약:
- key: `ACHIEVE_MESSAGE`
- `RUNTIME_*`: `args[0] = AchieveRuntime`
- `RUNTIME_REWARDED`: `args[1] = RewardData[]`
- `ACHIEVEMENT_UNLOCKED`: `args[0] = string achievementId`

---

## Related

- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
