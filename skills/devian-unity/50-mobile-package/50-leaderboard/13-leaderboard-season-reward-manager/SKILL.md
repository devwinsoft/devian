# 13-leaderboard-season-reward-manager

Status: DEPRECATED
AppliesTo: v10
Type: Design / Runtime Orchestration

시즌 전환 보상 평가/지급 로직은 `LeaderboardManager`로 통합되었다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Leaderboard/LeaderboardManager.cs`
- Login 연동:
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Login/LoginManager.cs`
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Login/LoginManager.cs`

---

## Ownership

- `LeaderboardManager`가 `LeaderboardSeasonRewardStorage`를 소유한다.
- 시즌 보상 지급 여부 판단의 SSOT는 `LeaderboardManager.Storage.processedClaims`다.

---

## Public API

- `LeaderboardManager.SyncSeasonTransitionRewardsAsync(ct)` -> `Task<CommonResult>`

---

## Core Flow

1. 초기화 상태 확인
2. 서버 시간 확보 (`RemoteDataManager.ServerNowUtcMs`)
3. `TB_LEADERBOARD`에서 active season row 수집 (`LEADERBOARD.seasonId → TB_SEASON`으로 시간 조회)
4. 모드별(`LEADERBOARD_MODE`) current season / previous season 계산 (TB_SEASON 시간 기준)
5. grace period 통과 여부 확인
6. `processedClaims` 중복 체크 (`{leaderboardId}`)
7. `LeaderboardManager.GetPlayerSnapshotAsync(...)` 조회
8. snapshot status별 분기
9. rank 기준 `TB_LEADERBOARD_REWARD.GetByGroup(leaderboardId)` 매칭
10. `RewardManager.ApplyRewardGroup(rewardGroupId)` 수행
11. claim 기록 후 `SaveDataManager.SaveGameStorageAsync(true, ct)` 저장

---

## Hard Rules

### 1) Grace Period는 상수만 사용

- `SeasonRewardGracePeriod = TimeSpan.FromMinutes(10)`
- `IsSeasonRewardEvaluationReady(prevSeasonEndUtcMs, serverNowUtcMs)`로만 판정
- magic number 금지

### 2) processedClaims 단일 기준

- claim key: `{leaderboardId}`
- 중복 지급 방지 기준은 해당 키 존재 여부 단일 체크
- 별도 `lastProcessedSeasonId` 상태를 두지 않는다

### 3) Snapshot 상태 처리

- `PlatformUnavailable` / `NotLoggedIn` / `Failed`: 기록하지 않고 재시도
- `NoScore`: `NO_PARTICIPATION` 기록
- `Success + rank 미매칭`: `RANK_OUT_OF_REWARD` 기록
- `Success + rank 매칭`: reward 지급 후 `CLAIMED` 기록

### 4) 보상 소스는 LEADERBOARD_REWARD

- `rewardGroupId`는 `LEADERBOARD_REWARD` 구간 매칭 결과로만 결정
- 하드코딩 보상/직접 RewardData 생성 금지

### 5) 스케줄러 없음

- period 개념이 아니라 시즌 경계 평가이므로 별도 scheduler를 두지 않는다
- 로그인 완료 경로에서 sync를 호출한다
- 추가 평가가 필요하면 상위 앱 로직이 `SyncSeasonTransitionRewardsAsync`를 명시 호출한다

---

## Test Boundary

- 시즌 종료 + 9분: 미평가
- 시즌 종료 + 10분: 평가 시작

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [14-leaderboard-season-reward-storage](../14-leaderboard-season-reward-storage/SKILL.md)
- [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [11-mobile-application](../../11-mobile-application/SKILL.md)
