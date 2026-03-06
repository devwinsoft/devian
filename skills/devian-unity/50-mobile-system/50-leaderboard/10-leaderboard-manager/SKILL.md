# 10-leaderboard-manager


Status: ACTIVE
AppliesTo: v10


MobileSystem 샘플에서 사용할 LeaderboardManager(설계)의 위치/역할/규약을 정의한다.
이 문서는 **구현이 아닌 설계 문서**다.


---


## Implementation Location (3-path mirror, 정본)


- UPM (정본):
  `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- asmdef:
  `Devian.Samples.MobileSystem`


---


## Public API (정본 규약)


- `InitializeAsync(ct)` → `Task<CommonResult>`
  - 명시적 호출 필수, Idempotent
- `ReportScoreAsync(leaderboardId, score, ct)` → `Task<CommonResult>`
- `UnlockAchievementAsync(achievementId, ct)` → `Task<CommonResult>`
- `SyncAsync(ct)` → `Task<CommonResult>`
  - 플랫폼에서 업적 상태를 읽고, "신규 달성"만 이벤트 발생


---


## Public Boundary (플랫폼 비노출 규약)


- `LeaderboardManager`의 `public/protected` 시그니처는 내부 ID(`leaderboardId`, `achievementId`)와 공통 타입(`CommonResult`)만 사용한다.
- 외부 API에서 플랫폼 전용 식별자(`apple*Id`, `google*Id`)를 입력/출력하지 않는다.
- 외부 API에서 플랫폼 SDK 타입(`Google.Play.Games.*`, `UnityEngine.SocialPlatforms.GameCenter.*`)을 사용하지 않는다.
- 플랫폼별 분기/매핑/SDK 호출은 Manager 내부 협력 객체(`internal/private`)로 숨긴다.


---


## Internal Adapter Contract (정본, internal)


- `ILeaderboardPlatformAdapter.InitializeAsync(ct)`
- `ILeaderboardPlatformAdapter.ReportScoreAsync(platformLeaderboardId, score, ct)`
- `ILeaderboardPlatformAdapter.UnlockAchievementAsync(platformAchievementId, kind, stepsTotal, ct)`
- `ILeaderboardPlatformAdapter.FetchAchievementStatesAsync(ct)`
  - 반환: `platformAchievementId -> unlocked(bool)` 맵
- 위 계약은 `internal` 범위로만 사용하며 상위 로직/외부 API에 노출하지 않는다.


---


## Events (Reward 연동 포인트)


- `OnAchievementUnlocked(achievementId)`
  - Reward 시스템(또는 상위 로직)이 구독하여 보상을 지급한다.
  - 이벤트 소비자 멱등 키 정본은 [03-ssot](../03-ssot/SKILL.md)의 `achievement:{achievementId}` 규칙을 따른다.


---


## Hard Rules (샘플은 반드시 준수)


- 상위 로직은 내부 ID만 사용(플랫폼 ID 직접 사용 금지)
- Reward 지급 로직을 LeaderboardManager에 넣지 않는다
- Editor/미지원 플랫폼에서 안전 실패(CommonResult 기반)로 종료한다
- `UnlockAchievementAsync`와 `SyncAsync`를 연속 호출해도 동일 업적 이벤트는 1회만 발생해야 한다

정본: [01-policy](../01-policy/SKILL.md)
