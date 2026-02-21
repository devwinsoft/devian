# 48-mission-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Mission(일일/주간/업적) 시스템의 모듈 경계와 하드룰을 정의한다.


---


## Hard Rules


### 1) 조건 평가는 MissionManager가 책임진다

- `MISSION` 테이블의 조건을 해석하고, 완료 여부를 판단하는 것은 MissionManager의 책임이다.
- LeaderboardManager는 플랫폼 업적/리더보드 연동만 담당하며, 일반 미션 조건 평가를 담당하지 않는다.


### 2) 보상 지급 기록/중복 방지/리셋은 MissionManager가 책임진다

- MissionManager는 "완료/클레임" 뿐 아니라, **지급 여부 조회/기록(로컬 ledger)/개별 지급 기록/리셋**을 책임진다.
- RewardManager는 **보상 지급 실행(Apply)** 만 수행한다. (멱등/기록/복구 책임 없음)

연관:
- [49-reward-system](../../49-reward-system/00-overview/SKILL.md)
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)


### 3) grantId는 Mission 반복 지급을 표현해야 한다

- 일일/주간 미션은 기간이 바뀌면 같은 missionId라도 다시 지급 가능해야 한다.
- 따라서 MissionManager는 category/resetRule에 따라 `grantId`를 생성해야 한다.
- `grantId` 규칙 정본은 [03-ssot](../03-ssot/SKILL.md)이다.
- daily/weekly 기간 키(날짜/주차) 및 리셋 경계는 **서버 시간 기준**으로 계산한다(디바이스 로컬 시간 기준 금지).
- NEEDS CHECK: 서버 시간 소스/획득 방식은 구현 단계에서 확정한다.


### 4) 저장/복구는 SaveDataManager 규약을 따른다

- 미션 진행도/완료/클레임 상태는 로컬 저장을 전제로 한다.
- 저장 책임의 큰 틀은 `21-savedata-system` 규약을 따른다.

연관: [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
