# 10-leaderboard-manager


Status: ACTIVE
AppliesTo: v10


MobileSystem 샘플에서 사용할 LeaderboardManager(설계)의 위치/역할/규약을 정의한다.
이 문서는 **구현이 아닌 설계 문서**다.


---


## Implementation Location (계획) — NEEDS CHECK


구현 단계에서 아래 위치로 생성하는 것을 목표로 한다(현재 파일은 없음).

- UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`
- UnityExample(미러): `framework-cs/apps/UnityExample/.../MobileSystem/Runtime/Leaderboard/LeaderboardManager.cs`

- asmdef(참고): `Devian.Samples.MobileSystem`


---


## Public API (설계)


- `InitializeAsync(ct)` → `Task<CommonResult>`
  - 명시적 호출 필수, Idempotent
- `ReportScoreAsync(leaderboardId, score, ct)` → `Task<CommonResult>`
- `UnlockAchievementAsync(achievementId, ct)` → `Task<CommonResult>`
- `SyncAsync(ct)` → `Task<CommonResult>`
  - 플랫폼에서 업적 상태를 읽고, "신규 달성"만 이벤트 발생


---


## Events (Reward 연동 포인트)


- `OnAchievementUnlocked(achievementId)`
  - Reward 시스템(또는 상위 로직)이 구독하여 보상을 지급한다.
  - 멱등 키는 [03-ssot](../03-ssot/SKILL.md)의 `grantId` 규칙을 따른다.


---


## Hard Rules (샘플은 반드시 준수)


- 상위 로직은 내부 ID만 사용(플랫폼 ID 직접 사용 금지)
- Reward 지급 로직을 LeaderboardManager에 넣지 않는다
- Editor/미지원 플랫폼에서 안전 실패(CommonResult 기반)로 종료한다

정본: [01-policy](../01-policy/SKILL.md)
