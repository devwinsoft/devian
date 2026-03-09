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
  - `MetaMessageManager.Storage.stats[messageId]`
- 허용 saveType:
  - `TB_MESSAGE.saveType == TOTAL_SUM || TOTAL_MAX`
- 허용되지 않는 saveType이면:
  - 점수 `0` 반환
  - error log 기록
- score가 `long.MaxValue` 초과면 clamp 후 전송한다.

## Season Time Gate

- `ReportScoreAsync`는 점수 제출 전 시즌 활성 기간을 확인한다.
- 시즌 시간 조회: `LEADERBOARD.seasonId → TB_SEASON.Get(seasonId) → StartUtcTime/EndUtcTime`
- 조건: `SEASON.StartUtcTime <= serverNowUtcMs < SEASON.EndUtcTime`
- 시즌 외 기간이면 `CommonResult.Failure` 반환.
- `seasonId`가 비어 있으면 시간 제한을 적용하지 않는다.

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
- 시즌 외 점수 기록 금지 (시즌 활성 기간에만 `ReportScoreAsync` 성공)

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [45-meta-message-system/10-meta-message-manager](../../45-meta-message-system/10-meta-message-manager/SKILL.md)
