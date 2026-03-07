# 03-ssot — 46-achieve-system

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Achieve 시스템의 정본이다.

- 업적 runtime 테이블: `ACHIEVE`
- 진행 stat 정본: `MESSAGE`
- 업적 알림 enum: `ACHIEVE_MESSAGE`
- 플랫폼 업적 매핑: `ACHIEVE.missionId -> (appleAchievementId, googleAchievementId)`
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
| `missionId` | string | 업적 그룹/내부 업적 ID |
| `isActive` | bool | 운영 토글 |
| `orderNum` | int | 정렬 기준(1-base) |
| `messageId` | string | `MESSAGE.messageId` FK |
| `level` | int | 단계 |
| `conditionValue` | `class:CBigInt` | 목표값 |
| `rewardGroupId` | string | claim 보상 키 |
| `appleAchievementId` | string | Game Center ID (missionId group 공통) |
| `googleAchievementId` | string | GPGS ID (missionId group 공통) |

---

## B. Runtime/Storage Rules

`AchieveStorage`:
- `schemaVersion`
- `nextAchieveUid`
- `runtimes: Dictionary<int, AchieveRuntime>`
- `stats: Dictionary<string, CBigInt>`

`AchieveRuntime` 저장 필드:
- `missionId`, `messageId`, `achieveUid`, `level`, `progressValue`, `isCompleted`

규칙:
- period 개념 없음 (`periodKey` 없음)
- group(`missionId`)당 runtime 1개 유지
- level-up 시 `achieveUid` 유지
- progress source of truth는 `stats[messageId]`

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
