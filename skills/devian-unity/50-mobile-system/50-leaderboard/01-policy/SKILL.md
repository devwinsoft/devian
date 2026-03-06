# 50-leaderboard — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Leaderboard/Achievements 연동의 **모듈 경계**와 **하드룰**,
그리고 샘플 매니저가 따라야 할 API 규약을 정의한다.

- Apple: Game Center
- Google: Google Play Games Services (GPGS v2)


---


## Hard Rules


### 1) 상위 로직에는 내부 ID만 노출한다


- 상위 로직은 `leaderboardId`, `achievementId`(내부 표준 ID)만 사용한다.
- Apple/Google의 플랫폼 문자열 ID는 "매핑 레이어(SSOT)"에만 존재해야 한다.

정본: [03-ssot](../03-ssot/SKILL.md)


### 1-1) Google/Apple 플랫폼 의존성은 외부 API에 노출하지 않는다


- `public/protected` API, 이벤트, DTO, 직렬화 payload에는 플랫폼 전용 타입/필드를 노출하지 않는다.
- 금지 예시:
  - `appleLeaderboardId`, `googleLeaderboardId`를 외부 입력/출력으로 노출
  - `Google.Play.Games.*`, `UnityEngine.SocialPlatforms.GameCenter.*` 타입을 외부 시그니처에 사용
- 허용 위치:
  - SSOT 테이블 데이터(`apple*Id`, `google*Id`)
  - 내부 플랫폼 어댑터 구현(`internal/private`)

정본: [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md)


### 2) Reward 지급은 Leaderboard의 책임이 아니다


- Leaderboard는 "업적 달성 여부 확인/동기화/보고"까지만 책임진다.
- "업적 달성 → 보상 지급/저장/중복 방지"는 Reward 시스템(또는 상위 로직/SaveData)이 책임진다.
- Leaderboard는 Reward 측이 소비할 수 있도록 **업적 달성 신호(event)** 만 제공한다.

정본: [03-ssot](../03-ssot/SKILL.md)


### Reward 연동은 이벤트로만 한다

- LeaderboardManager는 `OnAchievementUnlocked(achievementId)` 이벤트만 제공한다.
- 보상 지급/중복 방지는 이벤트 소비자(예: MissionManager 또는 상위 로직)가 책임진다.
- RewardManager는 "지급 실행(Apply)"만 담당한다(멱등/기록/복구 책임 없음).

연관:
- [48-mission-system/01-policy](../../48-mission-system/01-policy/SKILL.md)
- [49-reward-system/01-policy](../../49-reward-system/01-policy/SKILL.md)


### 3) Initialize는 명시적 호출이며, Awake 자동 초기화 금지


- Awake/OnEnable 등에서 자동으로 초기화하지 않는다.
- `InitializeAsync(ct)`는 Idempotent(중복 호출 안전)해야 한다.
- 초기화 이전 API 호출은 실패로 반환한다.

정본: [10-leaderboard-manager](../10-leaderboard-manager/SKILL.md)


### 4) 미지원 플랫폼/에디터는 안전 실패로 처리한다


- Editor, 미지원 플랫폼에서는 "예외/로그 폭발"이 아니라 **정해진 실패 결과**로 반환한다.
- `CommonResult` + `CommonErrorType`로 통일한다.
- `TB_ERROR_COMMON`에 아래 오류 코드를 추가해 Leaderboard 정본 에러 세트로 사용한다.
  - `LEADERBOARD_INIT_REQUIRED`
  - `LEADERBOARD_UNSUPPORTED_PLATFORM`
  - `LEADERBOARD_PLATFORM_NOT_FOUND`
  - `LEADERBOARD_AUTH_REQUIRED`
  - `LEADERBOARD_MAPPING_NOT_FOUND`
  - `LEADERBOARD_INVALID_SCORE`
  - `LEADERBOARD_PLATFORM_CALL_FAILED`
  - `LEADERBOARD_SYNC_FAILED`
- Leaderboard 공개 API는 `LOGIN_GPGS_*`, `LOGIN_APPLE_*` 같은 플랫폼 직접 명명 에러를 그대로 외부로 노출하지 않는다.


### 5) 업적 동기화(Sync)는 "신규 달성"만 신호를 발생시킨다


- 플랫폼에서 이미 달성된 업적을 Sync로 읽어왔을 때:
  - "이번 Sync에서 새로 달성으로 전환된 업적"만 `OnAchievementUnlocked(achievementId)`를 발생시킨다.
- 신호는 이벤트 소비자 측(MissionManager/상위 로직)이 멱등 처리(중복 지급 방지)할 수 있어야 한다.

정본: [09-ssot-operations](../09-ssot-operations/SKILL.md)


### 6) v1 업적 보고는 "완료 보고"만 지원한다


- 상위 로직은 업적 진행률이 아니라 "달성 완료 여부"만 판단해서 호출한다.
- `UnlockAchievementAsync(achievementId, ct)`는 완료 업적 보고 API다.
- `ACHIEVEMENT.kind=Percent/Steps`도 v1에서는 플랫폼에 완료 상태(100%)만 보고한다.
- 증분 진행률 API(`ReportAchievementProgressAsync` 등)는 v1 범위에서 제외한다.


---


## Client API (확정 규약)


> 클래스명/파일명은 구현 단계에서 정해도 되지만, 의미/경계는 아래 규약을 고정한다.


### 최소 기능


- `InitializeAsync(ct)` → `Task<CommonResult>`
- `ReportScoreAsync(leaderboardId, score, ct)` → `Task<CommonResult>`
- `UnlockAchievementAsync(achievementId, ct)` → `Task<CommonResult>`
- `SyncAsync(ct)` → `Task<CommonResult>`
  - 업적 상태를 플랫폼에서 읽어 "신규 달성 업적"을 판별한다.
- `ShowLeaderboardUi*` / `ShowAchievementsUi*`는 v1 공개 API에 포함하지 않는다.


### 이벤트(Reward 연동 포인트)


- `OnAchievementUnlocked(achievementId)`
  - 업적이 **신규로 달성**되었음을 의미한다.
  - 이벤트 소비자 측 중복 방지 키 정본은 `achievement:{achievementId}`다.

정본: [03-ssot](../03-ssot/SKILL.md)
