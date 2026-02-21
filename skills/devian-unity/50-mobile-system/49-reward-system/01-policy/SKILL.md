# 49-reward-system — Policy


Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point


## Purpose


Reward 시스템의 모듈 경계/하드룰/API 규약을 정의한다.

- Reward는 "보상 지급 실행(Apply)"만 담당한다.
- 멱등/기록/복구는 Reward 밖(호출자)에서 수행한다:
  - Mission(무료): MissionManager가 로컬 ledger/리셋/중복 방지를 책임진다.
  - Purchase(유료): PurchaseManager(+서버)가 멱등/기록/복구를 책임진다.


---


## Hard Rules


### 1) RewardManager는 지급 실행기다 (ledger/멱등/복구 금지)

- RewardManager는 "지급 적용(재화/아이템/플래그 반영)"만 수행한다.
- `grantId` 기반 멱등, 지급 기록 저장, pending 복구는 RewardManager 책임이 아니다.


### 2) 멱등/기록/복구는 호출자가 책임진다

- Mission 경로: MissionManager가 `grantId` 단위로 "지급 여부/중복 방지/리셋"을 관리한다.
- Purchase 경로: PurchaseManager(+서버 verifyPurchase)가 "멱등/원장/복구(restore 포함)"를 관리한다.

연관:
- [48-mission-system/01-policy](../../48-mission-system/01-policy/SKILL.md)
- [30-purchase-system/03-ssot](../../30-purchase-system/03-ssot/SKILL.md)


### 3) RewardManager는 서버/네트워크에 의존하지 않는다

- Firebase Functions/Firestore 호출 금지.
- Account(uid) 유무에 따라 동작이 달라지는 설계 금지.


### 4) 입력 payload(grants) 규약을 따른다

- Reward는 `grants[]` 형태의 payload를 받아 적용한다.
- Purchase 시스템의 `verifyPurchase` 응답 `grants[]`와 동일한 형태를 사용한다.

정본: [03-ssot](../03-ssot/SKILL.md)


---


## Client API (설계)

> 구현은 이후. 여기서는 "규약"만 확정한다.


### 최소 API

- `ApplyRewardDatas(deltas)`
  - `RewardData[]`를 로컬 인벤토리에 적용한다.
- `ApplyRewardGroupId(rewardGroupId)`
  - rewardGroupId를 `ResolveRewardDeltas(rewardGroupId)`(추상/override 강제)로 `RewardData[]`를 만든 뒤 적용한다.
  - rewardGroupId의 정본/해석 규칙은 컨텐츠 레이어에서 정의한다.


---


## Integration Notes

- Purchase: 서버 검증 결과가 `GRANTED`일 때만 컨텐츠 레이어 매핑(`internalProductId -> rewardGroupId`)을 거쳐 RewardManager의 `ApplyRewardGroupId(rewardGroupId)`를 호출한다.
- Mission: MissionManager가 로컬 ledger 기준으로 "지급 결정/중복 방지" 후 RewardManager에 `rewardGroupId` 지급 실행을 위임한다.
