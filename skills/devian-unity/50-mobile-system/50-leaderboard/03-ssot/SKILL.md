# 03-ssot — 50-leaderboard

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Leaderboard 점수 + 시즌 보상 시스템의 정본이다.

- 내부 리더보드 ID
- 플랫폼 리더보드 ID 매핑
- 시즌 기간/모드
- 구간별 보상 매핑(`LEADERBOARD_REWARD`)
- 시즌 보상 저장(`processedClaims`)

업적 SSOT는 `46-achieve-system`이 정본이다.

---

## A. Table Source

- 파일: `input/Domains/Game/MetaTable.xlsx`
- 시트: `LEADERBOARD`, `LEADERBOARD_REWARD`

---

## B. LEADERBOARD Schema

| field | type | note |
|------|------|------|
| `leaderboardId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `messageId` | string | 점수 소스 MESSAGE key |
| `appleLeaderboardId` | string | Game Center ID |
| `googleLeaderboardId` | string | GPGS ID |
| `mode` | `LEADERBOARD_MODE` | `NORMAL`/`HARDCORE` |
| `seasonId` | string | `SEASON.seasonId` FK — 시즌 기간은 TB_SEASON에서 참조 |

---

## C. LEADERBOARD_REWARD Schema

| field | type | note |
|------|------|------|
| `index` | int (pk) | row key |
| `leaderboardId` | string (group) | `LEADERBOARD.leaderboardId` 참조 |
| `rankFrom` | long | 포함 시작 순위 |
| `rankTo` | long | 포함 끝 순위 |
| `rewardGroupId` | string | RewardManager 지급 키 |

제약:
- `rankFrom >= 1`
- `rankTo >= rankFrom`
- 동일 `leaderboardId`에서 rank 구간 중복 금지
- `rewardGroupId`는 비어 있지 않아야 한다.

---

## D. Enum

- `LEADERBOARD_MODE`
  - `NONE`, `NORMAL`, `HARDCORE`
- `LeaderboardPlayerSnapshotStatus`
  - `Success`, `NoScore`, `PlatformUnavailable`, `NotLoggedIn`, `Failed`
- `LeaderboardSeasonRewardResultType`
  - `NONE`, `CLAIMED`, `NO_PARTICIPATION`, `RANK_OUT_OF_REWARD`

---

## E. Runtime / Storage SSOT

- 점수 제출 소스: `LEADERBOARD.messageId -> TB_GAME_MESSAGE -> GameMessageStorage.stats[messageId]`
- 시즌 시간 조회: `LEADERBOARD.seasonId → TB_SEASON.Get(seasonId) → StartUtcTime/EndUtcTime`
- score 허용 saveType: `TOTAL_SUM`, `TOTAL_MAX` (그 외는 0 + error log)
- 시즌 보상 저장:
  - payload key: `leaderboardReward`
  - core map: `processedClaims: Dictionary<string, LeaderboardSeasonRewardClaimRecord>`
  - key format: `{leaderboardId}`

`LeaderboardSeasonRewardClaimRecord` 필드:
- `resultType`
- `rank`
- `score`
- `rewardGroupId`
- `evaluatedAtServerUtcMs`

---

## F. Season Reward Gate

- 평가 시작 시점: `serverNowUtcMs >= prevSeasonEndUtcMs + SeasonRewardGracePeriod` (prevSeasonEndUtcMs는 TB_SEASON 참조)
- 정책 상수: `SeasonRewardGracePeriod = TimeSpan.FromMinutes(10)`
- magic number(예: `600000`, `45분`) 사용 금지

---

## G. Public Boundary

- 플랫폼 ID는 매핑 전용 데이터이며 외부 API payload에 노출하지 않는다.
- 시즌 보상 지급 결과는 `processedClaims`만 SSOT로 사용한다.

---

## H. Table Composition (MetaTable.xlsx)

시즌 보상 경로에서 사용하는 핵심 시트 구성도:

| Sheet | PK | 주요 FK/Group | 역할 |
|------|----|---------------|------|
| `MESSAGE` | `messageId` | - | 메시지 타입/저장 방식(`saveType`) 정의 |
| `MISSION` | `missionId` | `messageId -> MESSAGE.messageId` | 일일 미션 정의 및 조건 |
| `ACHIEVE_ONCE` | `index` | `conditionMsgId/reqMsgId -> MESSAGE.messageId` | 일반 업적 단계 정의 및 조건 |
| `ACHIEVE_PASS` | `index` | `conditionMsgId -> MESSAGE.messageId` | 패스 업적 단계 정의 및 pass 조건 |
| `SEASON` | `seasonId` | - | 시즌 기간 정의 (`StartUtcTime`/`EndUtcTime`) |
| `LEADERBOARD` | `leaderboardId` | `messageId -> MESSAGE.messageId`, `seasonId -> SEASON.seasonId` | 점수 소스 + 시즌 참조 + 플랫폼 ID 매핑 |
| `LEADERBOARD_REWARD` | `index` | `leaderboardId (group)` | 랭크 구간별 `rewardGroupId` 매핑 |

관계 흐름:

```text
GAME_MESSAGE -> MESSAGE(messageId)
                    ├─ MISSION.messageId
                    ├─ ACHIEVE_ONCE.conditionMsgId
                    ├─ ACHIEVE_ONCE.reqMsgId
                    ├─ ACHIEVE_PASS.conditionMsgId
                    └─ LEADERBOARD.messageId

SEASON(seasonId, StartUtcTime, EndUtcTime)
        └─ LEADERBOARD(leaderboardId, mode, seasonId -> SEASON)
                └─ LEADERBOARD_REWARD(leaderboardId group, rankFrom~rankTo, rewardGroupId)
```

시즌 보상 평가 시 사용 경로:

1. `LEADERBOARD`에서 current/previous season row 결정
2. previous row의 `leaderboardId`로 `LEADERBOARD_REWARD` 구간 조회
3. player rank를 구간 매칭해 `rewardGroupId` 결정
4. `RewardManager.ApplyRewardGroup(rewardGroupId)` 실행

---

## I. Record Time Limit

- 점수 기록(`ReportScoreAsync`)은 시즌 활성 기간에만 허용한다.
- 조건: `SEASON.StartUtcTime <= serverNowUtcMs < SEASON.EndUtcTime`
- 시즌 외 기간 → `CommonResult.Failure` 반환
- `seasonId`가 비어 있으면 시간 제한 없음
