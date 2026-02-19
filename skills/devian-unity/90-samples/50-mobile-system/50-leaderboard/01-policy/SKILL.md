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
- (구현 단계) `CommonResult` + `CommonErrorType`로 통일한다.

NEEDS CHECK(구현 단계): Leaderboard 전용 error code 세트 확정


### 5) 업적 동기화(Sync)는 "신규 달성"만 신호를 발생시킨다


- 플랫폼에서 이미 달성된 업적을 Sync로 읽어왔을 때:
  - "이번 Sync에서 새로 달성으로 전환된 업적"만 `OnAchievementUnlocked(achievementId)`를 발생시킨다.
- 신호는 이벤트 소비자 측(MissionManager/상위 로직)이 멱등 처리(중복 지급 방지)할 수 있어야 한다.

정본: [09-ssot-operations](../09-ssot-operations/SKILL.md)


---


## Client API (권장 형태)


> 이 섹션은 "규약"이며, 실제 구현 클래스명/시그니처는 구현 단계에서 확정한다.


### 최소 기능


- `InitializeAsync(ct)` → `Task<CommonResult>`
- `ReportScoreAsync(leaderboardId, score, ct)` → `Task<CommonResult>`
- `UnlockAchievementAsync(achievementId, ct)` → `Task<CommonResult>`
- `SyncAsync(ct)` → `Task<CommonResult>`
  - 업적 상태를 플랫폼에서 읽어 "신규 달성 업적"을 판별한다.


### 이벤트(Reward 연동 포인트)


- `OnAchievementUnlocked(achievementId)`
  - 업적이 **신규로 달성**되었음을 의미한다.
  - 이벤트 소비자 측이 자체 ledger 키(예: `achievement:{achievementId}` 형태)를 사용해 중복 방지를 수행한다(구체 규칙은 소비자 SSOT에서 정의).

정본: [03-ssot](../03-ssot/SKILL.md)
