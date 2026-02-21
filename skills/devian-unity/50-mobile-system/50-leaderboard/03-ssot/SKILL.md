# 03-ssot — 50-leaderboard (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

이 문서는 50-leaderboard의 **단일 SSOT 허브**이다.
운영/테스트/DoD는 [09-ssot-operations](../09-ssot-operations/SKILL.md)를 참조한다.

- 내부 ID(leaderboardId / achievementId)
- 플랫폼 ID 매핑(Apple/Google)
- Achievement → Reward 연동 규약(트리거 정본)


## 우선순위

- Root SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- Unity 관련 SSOT: `skills/devian-unity/03-ssot/SKILL.md`
- Leaderboard/Achievements 관련 SSOT: 이 문서

충돌 시 Root SSOT의 정의/플레이스홀더/규약이 우선한다.


---


## A. Internal IDs (정본)


- `leaderboardId`: 게임 내부 표준 리더보드 ID (string)
- `achievementId`: 게임 내부 표준 업적 ID (string)

상위 로직은 위 내부 ID만 사용한다.


---


## B. Platform ID Mapping (정본)


내부 ID ↔ 플랫폼 문자열 ID 매핑이 반드시 필요하다.

- Apple(Game Center): `appleLeaderboardId`, `appleAchievementId`
- Google(GPGS v2): `googleLeaderboardId`, `googleAchievementId`


### NEEDS CHECK (구현 단계에서 SSOT 저장 위치 결정)

아래 중 하나를 SSOT로 선택한다(문서상 설계만 정의).

- (권장) Devian input 테이블(Excel) 기반
  - 예: `input/Domains/Leaderboard/LeaderboardTable.xlsx`
  - `input/input_common.json`에 DomainKey 등록
- (대안) 샘플 코드에 const/JSON으로 내장 (초기 MVP)


---


## C. Suggested Table Schema (설계)


### 1) LEADERBOARD (내부 리더보드 정의)


| field | type | note |
|------|------|------|
| `leaderboardId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `appleLeaderboardId` | string | Game Center ID |
| `googleLeaderboardId` | string | GPGS ID |
| `scoreOrder` | string | `"HighBetter"` / `"LowBetter"` (표현만, 구현은 구현 단계) |


### 2) ACHIEVEMENT (내부 업적 정의)


| field | type | note |
|------|------|------|
| `achievementId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `appleAchievementId` | string | Game Center ID |
| `googleAchievementId` | string | GPGS ID |
| `kind` | string | `"Binary"` / `"Percent"` / `"Steps"` (표현만, 구현은 구현 단계) |
| `stepsTotal` | int | kind=Steps일 때만 사용 |


---


## D. Achievement Unlocked Event (정본)


Leaderboard는 Reward를 직접 지급하지 않는다.
Reward 연동은 "이벤트 신호"로만 한다.


### 1) 이벤트 신호(정본)

- `OnAchievementUnlocked(achievementId)`
    - "신규로 달성된 업적"에 대해서만 발생한다(이미 달성은 발생 금지).


### 2) (선택) 소비자 ledger 키 규칙

- 이벤트 소비자(MissionManager/상위 로직)가 중복 방지를 위해 자체 키를 사용할 수 있다.
- 예: `achievement:{achievementId}`


### 3) (선택) Achievement → RewardKey 매핑(정본)

- key: `achievementId`
- value: `rewardKey`

이 매핑은 **이벤트 소비자**가 참조한다(RewardManager는 멱등/트리거 해석 책임 없음).

연관 SSOT:
- Mission(업적 미션으로 운영 시): [48-mission-system/03-ssot](../../48-mission-system/03-ssot/SKILL.md)
- Reward(지급 실행): [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
