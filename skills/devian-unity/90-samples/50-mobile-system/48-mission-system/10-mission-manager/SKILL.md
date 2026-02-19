# 10-mission-manager


MissionManager(설계)는 `MISSION` 테이블 기반으로 미션을 평가하고, 완료/클레임 상태를 관리한다.
daily/weekly 리셋 경계 및 기간 키(dailyKey/weeklyKey)는 **서버 시간 기준**으로 계산한다.
mission row의 `rewardId`는 컨텐츠 레이어의 reward 키를 사용하며, 실제 지급 실행(Apply)은 RewardManager에 위임한다(구현은 이후).


---


## Responsibilities (정본)

- `MISSION` 테이블 로드
- 조건 평가/진행도 누적
- 완료 판정 및 "클레임 가능" 상태 관리
- 서버 시간 기준 daily/weekly 리셋 처리
- 클레임 요청 시 `grantId` 생성 + `rewardId` 조회 후 RewardManager로 지급 실행 위임
- `grantId` 단위 로컬 ledger(pending/granted) 관리


---


## Dependencies (개념)

- Reward 지급: `49-reward-system` (RewardManager)
- 로컬 저장: `21-savedata-system/10-savedata-manager`
- (선택) 플랫폼 업적/리더보드 연동: `50-leaderboard`


---


## Public API (설계)

- `InitializeAsync(ct)`
    - 테이블 로드 + 저장 상태 로드 + (서버 시간 기준) 리셋 경계 처리
- `OnGameEvent(eventKey, value, ct)`
    - 조건 평가 입력(표현만, 구현은 이후)
- `TryClaimAsync(missionId, ct)`
    - `grantId` 생성 → 로컬 ledger 중복 체크/`pending` 기록 → `rewardId` 확보 → RewardManager로 지급 실행(ApplyRewardId 또는 deltas 적용) 위임 → 성공 시 `granted` 확정 저장


---


## Outputs (설계)

- `OnMissionCompleted(missionId)` (선택)
- `OnMissionClaimable(missionId)` (선택)
- UI는 저장 상태 + 테이블을 기반으로 렌더링한다(구현은 이후).
