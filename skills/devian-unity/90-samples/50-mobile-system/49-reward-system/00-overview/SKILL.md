# 49-reward-system — Overview


Status: ACTIVE
AppliesTo: v10


MobileSystem 샘플에서 Reward(보상 지급 **적용/실행**) 설계를 정의한다.

이 스킬 그룹의 핵심은 아래 한 줄이다.

- Reward는 **보상 지급 실행(Apply)** 만 담당한다.
- **멱등/기록/복구는 Reward의 책임이 아니며**, 각 트리거 소유자(MissionManager / PurchaseManager)가 책임진다.


---


## Scope

- 입력으로 들어온 **InventoryDelta[]** 또는 rewardId(컨텐츠 레이어에서 InventoryDelta로 해석된 결과)를 **로컬 인벤토리에 적용**한다.
- Reward는 "지급 실행기"이며, 서버 호출/ledger 확정/중복 방지는 하지 않는다.


## Non-goals

- 지급 이력(ledger) 정본/조회
- grantId 멱등 보장
- pending queue 저장/복구
- Firebase Functions/Firestore를 통한 서버 확정


---


## Start Here


| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계/하드룰(Reward=지급 실행기, 멱등/기록/복구=호출자) |
| [03-ssot](../03-ssot/SKILL.md) | InventoryDelta 규약 + rewardId 해석(컨텐츠 책임) |
| [09-ssot-operations](../09-ssot-operations/SKILL.md) | 운영 시나리오/테스트/DoD |
| [10-reward-manager](../10-reward-manager/SKILL.md) | RewardManager 설계(지급 실행기) |


---


## Related

- [48-mission-system](../../48-mission-system/00-overview/SKILL.md) — Mission(무료) 지급 기록/리셋 정본
- [30-purchase-system](../../30-purchase-system/00-overview/SKILL.md) — Purchase(유료) 멱등/기록/복구 정본
- [21-savedata-system](../../21-savedata-system/00-overview/SKILL.md)
