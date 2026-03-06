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
- Manager는 `knownUnlockedAchievementIds`(내부 set)를 갱신하며, set에 새로 추가되는 업적만 이벤트를 발생시킨다.
- 이벤트 소비자(MissionManager/상위 로직)가 이 이벤트를 소비해 중복 방지(자체 ledger) 후 RewardManager로 "지급 실행(Apply)"을 수행한다.

연관:
- [48-mission-system/09-ssot-operations](../../48-mission-system/09-ssot-operations/SKILL.md)


### 2) 업적 달성 시점

- 상위 로직이 "업적 달성 조건 충족"을 결정한다(플랫폼이 결정하지 않음).
- 즉시 `UnlockAchievementAsync(achievementId, ct)` 호출(플랫폼 반영)
- `UnlockAchievementAsync` 성공 시에도 내부 set 전이(`false -> true`)가 확인될 때만 `OnAchievementUnlocked(achievementId)`를 발생시킨다.
- 같은 업적에서 `UnlockAchievementAsync` 후 `SyncAsync`가 연속 호출되어도 이벤트는 1회만 발생해야 한다.
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
- Manager 이벤트 중복 방지:
  - 같은 업적에 대해 `UnlockAchievementAsync` + `SyncAsync` 연속 호출 시 이벤트가 1회만 발생
- 플랫폼 의존성 비노출:
  - Leaderboard 공개 API 시그니처/DTO에서 `apple`, `google`, `gpgs`, `gamecenter` 명칭 및 플랫폼 SDK 타입이 노출되지 않음


---


## DoD (구현 기준)


### Hard (반드시 0)

- 중복 지급(동일 achievementId) 0건 — `achievement:{achievementId}` 멱등 키 처리로 보장
- 초기화 전 API 호출 시 안전 실패(정해진 실패 결과) 0건 예외
- 미지원 플랫폼/Editor에서 크래시 0건
- Sync가 "신규 달성"만 이벤트 발생(이미 달성은 무시)
- 공개 API/DTO에 플랫폼 의존 타입/필드 노출 0건


### Soft

- 플랫폼 native UI(리더보드/업적 화면) 제공은 v1 범위 밖이며, 필요 시 별도 API 문서로 확장
