# 10-leaderboard-manager

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 `LeaderboardManager` 설계 문서다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`

---

## Public API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `ReportScoreAsync(leaderboardId, ct)` -> `Task<CommonResult>`
- `GetPlayerSnapshotAsync(leaderboardId, ct)` -> `Task<CommonResult<LeaderboardPlayerSnapshot>>`
- `SyncSeasonTransitionRewardsAsync(ct)` -> `Task<CommonResult>`
- `ClearStorage()`

obsolete:
- `ReportScoreAsync(leaderboardId, score, ct)` (deprecated shim)

---

## Internal Adapter Contract

- `InitializeAsync(ct)`
- `ReportScoreAsync(platformLeaderboardId, score, ct)`
- `LoadPlayerSnapshotAsync(platformLeaderboardId, internalLeaderboardId, ct)`

Adapter 계약은 `internal/private` 범위로만 사용한다.

---

## Score Resolution Rules

- 상위에서 점수를 직접 전달하지 않는다.
- `LEADERBOARD.messageId`를 기준으로 score를 내부 계산한다.
- score 소스:
  - `GameMessageManager.Storage.stats[messageId]`
- 허용 saveType:
  - `TB_MESSAGE.saveType == TOTAL_SUM || TOTAL_MAX`
- 허용되지 않는 saveType이면:
  - 점수 `0` 반환
  - error log 기록
- score가 `long.MaxValue` 초과면 clamp 후 전송한다.

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

- 외부 API는 내부 `leaderboardId`만 사용
- 업적 Unlock/Sync 로직 포함 금지 (`AchieveManager` 책임)
- 시즌 보상 claim 상태 저장/관리 책임은 `LeaderboardManager.Storage(LeaderboardSeasonRewardStorage)`가 가진다.
- 미지원 플랫폼 안전 실패

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [45-game-message-system/10-game-message-manager](../../45-game-message-system/10-game-message-manager/SKILL.md)
