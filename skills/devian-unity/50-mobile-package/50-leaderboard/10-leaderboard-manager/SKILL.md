# 10-leaderboard-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `LeaderboardManager` 설계 문서다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`

---

## Public API

- `InitializeAsync(ct)` -> `Task<GameResult>`
- `ReportScoreAsync(leaderboard_id, ct)` -> `Task<GameResult>`
- `GetPlayerSnapshotAsync(leaderboard_id, ct)` -> `Task<GameResult<LeaderboardPlayerSnapshot>>`
- `SyncSeasonTransitionRewardsAsync(ct)` -> `Task<GameResult>`
- `ClearStorage()`

초기화 규약:
- `LeaderboardManager`는 `MobileApplication.Instance`가 존재하면 Awake/Start에서 `InitializeAsync`를 자체 수행한다.
- `ReportScoreAsync`, `GetPlayerSnapshotAsync`, `SyncSeasonTransitionRewardsAsync`는 내부에서 초기화를 보장한다.
- 외부에서 `InitializeAsync`를 명시 호출해도 무방하지만 필수는 아니다.

obsolete:
- `ReportScoreAsync(leaderboard_id, score, ct)` (deprecated shim)

---

## Internal Adapter Contract

- `InitializeAsync(ct)`
- `ReportScoreAsync(platformLeaderboardId, score, ct)`
- `LoadPlayerSnapshotAsync(platformLeaderboardId, internalLeaderboardId, ct)`

Adapter 계약은 `internal/private` 범위로만 사용한다.

---

## Score Resolution Rules

- 상위에서 점수를 직접 전달하지 않는다.
- `LEADERBOARD.message_id`를 기준으로 score를 내부 계산한다.
- score 소스:
  - `GameMessageManager.Storage.stats[message_id]`
- 허용 saveType:
  - `TB_GAME_MESSAGE.saveType == TOTAL_SUM || TOTAL_MAX`
- 허용되지 않는 saveType이면:
  - 점수 `0` 반환
  - error log 기록
- score가 `long.MaxValue` 초과면 clamp 후 전송한다.

## Season Time Gate

- `ReportScoreAsync`는 점수 제출 전 시즌 활성 기간을 확인한다.
- 시즌 시간 조회: `LEADERBOARD.season_id → TB_SEASON.Get(season_id) → Start_utc_time/End_utc_time`
- 조건: `SEASON.Start_utc_time <= serverNowUtcMs < SEASON.End_utc_time`
- 시즌 외 기간이면 `GameResult.Failure` 반환.
- `season_id`가 비어 있으면 시간 제한을 적용하지 않는다.

---

## Snapshot Rules

`LeaderboardPlayerSnapshotStatus`:
- `Success`
- `NoScore`
- `PlatformUnavailable`
- `NotLoggedIn`
- `Failed`

season reward 평가는 위 status를 그대로 사용한다.

---

## Hard Rules

- 외부 API는 내부 `leaderboard_id`만 사용
- 업적 Unlock/Sync 로직 포함 금지 (`AchieveManager` 책임)
- 시즌 보상 claim 상태 저장/관리 책임은 `LeaderboardManager.Storage(LeaderboardSeasonRewardStorage)`가 가진다.
- 미지원 플랫폼 안전 실패
- 시즌 외 점수 기록 금지 (시즌 활성 기간에만 `ReportScoreAsync` 성공)

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [45-game-message-system/10-game-message-manager](../../45-game-message-system/10-game-message-manager/SKILL.md)
