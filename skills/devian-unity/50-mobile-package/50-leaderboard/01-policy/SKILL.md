# 50-leaderboard — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

Leaderboard 점수 제출 + 시즌 전환 보상 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) 상위 로직에는 내부 ID만 노출한다

- 외부 API는 `leaderboard_id`(내부 ID)만 사용한다.
- 플랫폼 문자열 ID는 SSOT 매핑 레이어에만 존재한다.

### 2) LeaderboardManager는 점수/스냅샷 + 시즌 보상을 책임진다

- `ReportScoreAsync` / `GetPlayerSnapshotAsync` / `SyncSeasonTransitionRewardsAsync`를 책임진다.
- 업적 Unlock/Sync는 `AchieveManager` 책임이다.
- 시즌 보상 평가/지급은 `LeaderboardManager` 내부 기능이다.

연관: [46-achieve-system/01-policy](../../46-achieve-system/01-policy/SKILL.md)

### 3) Season reward 지급 여부는 processedClaims 단일 기준이다

- claim dedupe 기준은 `processedClaims[leaderboard_id]` 단일 키다.
- `lastProcessedSeasonId` 같은 이중 상태를 두지 않는다.

### 4) grace period는 하드코딩 금지, 상수만 사용한다

- 시즌 보상 평가 게이트는 `SeasonRewardGracePeriod` 상수로만 계산한다.
- 현재 정책값: `TimeSpan.FromMinutes(10)`.

### 5) 보상 소스는 LEADERBOARD_REWARD만 사용한다

- 시즌 보상 지급은 `LEADERBOARD_REWARD.reward_group_id`를 통해서만 수행한다.
- 하드코딩 보상/직접 RewardData 조립 금지.

### 6) NoReward 기록 조건을 엄격히 유지한다

- `NoScore`(정상 조회 + 미참여)에서만 `NO_PARTICIPATION` 기록.
- `PlatformUnavailable` / `NotLoggedIn` / `Failed`는 claim을 기록하지 않고 재시도 경로로 둔다.

### 7) Initialize는 LeaderboardManager 내부에서 보장한다

- `LeaderboardManager`는 `MobileApplication.Instance`가 존재할 때 자체 초기화를 시도한다.
- `ReportScoreAsync`/`GetPlayerSnapshotAsync`/`SyncSeasonTransitionRewardsAsync`는 내부에서 초기화를 보장한다.

### 8) 미지원 플랫폼/에디터는 안전 실패

- 예외 폭발 없이 `CommonResult` 실패로 종료한다.

### 9) 공개 경계에서 플랫폼 의존 타입/필드 비노출

- 공개 API/DTO에 `apple*Id`, `google*Id`를 노출하지 않는다.
- 공개 API 시그니처에 플랫폼 SDK 타입을 노출하지 않는다.

### 10) 점수 기록은 시즌 활성 기간으로 제한한다

- `ReportScoreAsync`는 `LEADERBOARD.season_id` → `TB_SEASON` 조회 후 시간 범위를 확인한다.
- 조건: `SEASON.Start_utc_time <= serverNowUtcMs < SEASON.End_utc_time`
- 범위 밖이면 `CommonResult.Failure` 반환.
- `season_id`가 비어 있으면 시간 제한을 적용하지 않는다.

---

## Client API

`LeaderboardManager`
- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `ReportScoreAsync(leaderboard_id, ct)` -> `Task<CommonResult>`
- `GetPlayerSnapshotAsync(leaderboard_id, ct)` -> `Task<CommonResult<LeaderboardPlayerSnapshot>>`
- `SyncSeasonTransitionRewardsAsync(ct)` -> `Task<CommonResult>`
