# 10-leaderboard-manager

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 `LeaderboardManager` 설계 문서다.

---

## Implementation Location

- Assets/Samples:
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`

---

## Public API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `ReportScoreAsync(leaderboardId, score, ct)` -> `Task<CommonResult>`

---

## Internal Adapter Contract

- `InitializeAsync(ct)`
- `ReportScoreAsync(platformLeaderboardId, score, ct)`

Adapter 계약은 `internal/private` 범위로만 사용한다.

---

## Hard Rules

- 외부 API는 내부 `leaderboardId`만 사용
- 업적 Unlock/Sync 로직 포함 금지 (`AchieveManager` 책임)
- 미지원 플랫폼 안전 실패
