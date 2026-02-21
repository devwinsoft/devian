# 09-ssot-operations — 50-leaderboard


Status: ACTIVE
AppliesTo: v10


이 문서는 50-leaderboard의 운영/테스트/DoD 정본이다.
ID 매핑/트리거 규칙은 [03-ssot](../03-ssot/SKILL.md)가 정본이다.


---


## 운영 시나리오(정본)


### 1) 앱 시작/로그인 시

- `InitializeAsync(ct)`
- `SyncAsync(ct)`:
  - 플랫폼 업적 상태를 읽는다.
  - "신규 달성으로 전환된 업적"만 `OnAchievementUnlocked(achievementId)`를 발생시킨다.
- Sync로 "신규 달성"을 감지하면 `OnAchievementUnlocked(achievementId)`를 발생시킨다.
- 이벤트 소비자(MissionManager/상위 로직)가 이 이벤트를 소비해 중복 방지(자체 ledger) 후 RewardManager로 "지급 실행(Apply)"을 수행한다.

연관:
- [48-mission-system/09-ssot-operations](../../48-mission-system/09-ssot-operations/SKILL.md)
- [49-reward-system/09-ssot-operations](../../49-reward-system/09-ssot-operations/SKILL.md)


### 2) 업적 달성 시점

- 상위 로직이 "업적 달성 조건 충족"을 결정한다(플랫폼이 결정하지 않음).
- 즉시 `UnlockAchievementAsync(achievementId, ct)` 호출(플랫폼 반영)
- 성공 시(또는 Sync에서 신규 달성 판별 시) `OnAchievementUnlocked(achievementId)` 신호가 발생한다.
- Reward 지급은 이벤트 소비자(MissionManager/상위 로직)가 자체 ledger로 중복 방지 후 RewardManager로 지급 실행을 위임한다.


### 3) 다중 기기/재설치

- 다른 기기에서 이미 달성된 업적은 로그인 후 Sync에서 감지될 수 있다.
- 이 경우에도 이벤트 소비자(MissionManager/상위 로직)가 "기기 기준"이 아니라 "계정/SaveData 기준"으로 자체 ledger를 사용해 중복 지급을 방지해야 한다.


---


## 테스트 체크리스트(정본)


- Editor에서 안전 실패(예외/로그 폭발 없음)
- iOS(Game Center)에서:
  - 인증 성공/실패 케이스
  - ReportScore/UnlockAchievement 성공/실패 케이스
  - Sync에서 신규 달성만 이벤트 발생
- Android(GPGS v2)에서:
  - 플러그인 설치/미설치(컴파일 안전) 케이스
  - 인증 성공/실패 케이스
  - ReportScore/UnlockAchievement 성공/실패 케이스
  - Sync에서 신규 달성만 이벤트 발생
- 이벤트 소비자 중복 방지:
  - 동일 achievementId로 이벤트가 재발생해도 이벤트 소비자(MissionManager/상위 로직)의 자체 ledger 기준으로 중복 지급이 일어나지 않음


---


## DoD (구현 단계 기준)


### Hard (반드시 0)

- 중복 지급(동일 achievementId) 0건 — `grantId` 멱등 처리로 보장
- 초기화 전 API 호출 시 안전 실패(정해진 실패 결과) 0건 예외
- 미지원 플랫폼/Editor에서 크래시 0건
- Sync가 "신규 달성"만 이벤트 발생(이미 달성은 무시)


### Soft

- 플랫폼 UI(리더보드/업적 화면) 제공 여부는 제품 요구에 따라 선택
