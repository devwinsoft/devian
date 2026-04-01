# 49-reward-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Reward 시스템의 모듈 경계/하드룰/API 규약을 정의한다.

- Reward는 "보상 지급 실행(Apply)"만 담당한다.
- 멱등/기록/복구는 Reward 밖(호출자)에서 수행한다.


---


## Hard Rules


### 1) RewardManager는 지급 실행기다 (ledger/멱등/복구 금지)

- RewardManager는 "지급 적용(재화/아이템/플래그 반영)"만 수행한다.
- `grantId` 기반 멱등, 지급 기록 저장, pending 복구는 RewardManager 책임이 아니다.


### 2) 멱등/기록/복구는 호출자가 책임진다

- 호출자가 경로별로 `grantId` 멱등/원장/복구를 관리한다.


### 3) RewardManager는 서버/네트워크에 의존하지 않는다

- Firebase Functions/Firestore 호출 금지.
- Account(uid) 유무에 따라 동작이 달라지는 설계 금지.


### 4) 입력 payload(grants) 규약을 따른다

- Reward는 `grants[]` 형태의 payload를 받아 적용한다.
- 서버 검증 응답 `grants[]`와 동일한 형태를 사용한다.

정본: [03-ssot](../03-ssot/SKILL.md)


---


## Client API (설계)

> 구현은 이후. 여기서는 "규약"만 확정한다.


### 최소 API

- `ApplyRewardDatas(deltas)`
  - `RewardData[]`를 로컬 인벤토리에 적용한다.
- `ApplyRewardGroup(reward_group_id)`
  - rewardGroupId를 `ResolveRewardDeltas(reward_group_id)`(추상/override 강제)로 `RewardData[]`를 만든 뒤 적용한다.
  - rewardGroupId의 정본/해석 규칙은 컨텐츠 레이어에서 정의한다.
- `ApplyRewardGroup(reward_group_id)`
  - 실제 지급 + `RewardApplyResult.AppliedRewards` 반환이 필요한 호출부(호출 결과 payload 등)에서 사용한다.


