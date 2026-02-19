# 10-reward-manager


RewardManager(설계)는 입력된 **InventoryDelta[]** 또는 `rewardId`(컨텐츠 해석 결과)를 로컬 인벤토리에 **적용(지급 실행)** 한다.
멱등/기록/복구는 RewardManager의 책임이 아니다.


---


## Responsibilities (정본)

- `InventoryDelta[]` 입력을 받아 로컬 인벤토리에 적용
- (선택) `rewardId`(= Game `REWARD.rewardId(pk)`)를 컨텐츠 레이어에서 `InventoryDelta[]`로 해석한 뒤 적용

비책임(금지):
- `grantId` 멱등 처리
- 지급 기록(ledger) 저장/조회
- Firebase Functions/Firestore 호출
- pending queue/복구


---


## Dependencies (개념)

- InventoryManager(또는 프로젝트의 인벤토리 적용 구현)
  - RewardManager는 Inventory에 "아이템/통화 추가(+) 적용"을 위임한다.
- SaveDataManager ↔ InventoryManager 직접 결합은 금지(상위 조립에서만 결합).


---


## Public API (설계)

- `ApplyDeltasAsync(deltas, ct)`
- `(선택) ApplyRewardIdAsync(rewardId, ct)`


---


## Sequence Examples


### A) Purchase

1) PurchaseManager → 서버 `verifyPurchase`
2) 응답 `resultStatus == GRANTED`
3) PurchaseManager → RewardManager: `ApplyDeltasAsync(deltas)`
4) UI/표시는 서버 `entitlementsSnapshot` 기준으로 갱신


### B) Mission

1) MissionManager가 로컬 ledger에서 `grantId` 지급 여부 확인
2) 미지급이면 ledger를 `pending`으로 기록
3) MissionManager → RewardManager: `ApplyRewardIdAsync(rewardId)`
4) RewardManager: 컨텐츠 레이어에서 rewardId → InventoryDelta[] 해석 → `ApplyDeltasAsync(deltas)`
5) 성공 시 ledger를 `granted`로 확정, 실패 시 `pending` 유지(재시도)
