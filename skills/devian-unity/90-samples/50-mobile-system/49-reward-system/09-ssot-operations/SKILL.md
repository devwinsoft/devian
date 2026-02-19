# 09-ssot-operations — 49-reward-system


Status: ACTIVE
AppliesTo: v10


이 문서는 Reward의 운영/테스트/DoD 정본이다.
SSOT 규칙은 [03-ssot](../03-ssot/SKILL.md)가 정본이다.


---


## 운영 시나리오(정본)


### 1) Purchase 경로

- PurchaseManager가 서버 `verifyPurchase`를 호출한다.
- `resultStatus == GRANTED`일 때만:
  - 컨텐츠 레이어 매핑(`internalProductId -> rewardId`) 후 `ApplyRewardId(rewardId)`로 적용한다.
- 멱등/복구/원장 정본은 Purchase 쪽이다.

연관:
- [30-purchase-system/03-ssot](../../30-purchase-system/03-ssot/SKILL.md)
- [30-purchase-system/43-purchase-client-server-integration](../../30-purchase-system/43-purchase-client-server-integration/SKILL.md)


### 2) Mission 경로

- MissionManager가 로컬 ledger 기준으로 "지급 여부/중복 방지"를 판단한다.
- 지급하기로 결정되면 RewardManager에 적용을 위임한다.
- 리셋/중복 방지/재시도는 MissionManager 책임이다.

연관: [48-mission-system/09-ssot-operations](../../48-mission-system/09-ssot-operations/SKILL.md)


---


## 테스트 체크리스트(정본)

- RewardManager는 서버/계정(uid)에 의존하지 않는다(오프라인에서도 Apply는 가능)
- 동일 grants 입력에 대해 적용 결과가 결정적이다(동일 입력 → 동일 반영)
- RewardManager는 멱등/중복 방지를 하지 않는다(중복 방지는 호출자 테스트 항목)


---


## DoD (구현 단계 기준)


Hard (반드시 0)
- Reward 문서에서 "서버 확정/ledger/멱등/복구"를 Reward 책임으로 주장하는 문구 0개
- Purchase/Mission 문서와 책임 경계 충돌 0개

Soft
- grants type/id 규칙 확정(NEEDS CHECK 해소)
