# 10-reward-manager


RewardManager는 입력된 `rewardGroupId` 또는 **RewardData[]**를 로컬 인벤토리에 **적용(지급 실행)** 한다.

RewardManager는 **단일 concrete 클래스**이다.
- `rewardGroupId -> RewardData[]` 변환은 `TB_REWARD.GetByGroup()` 을 직접 참조하여 구현한다.
- 멱등/기록/복구는 RewardManager의 책임이 아니다.


---


## Singleton

```csharp
CompoSingleton<RewardManager>.Instance
```

- Registry key: `RewardManager`
- 다른 매니저에서 접근: `Singleton.Get<RewardManager>()`


---


## Responsibilities (정본)

- `RewardData[]` 입력을 받아 로컬 인벤토리에 적용
- `rewardGroupId`를 입력받아 `ResolveRewardDeltas(rewardGroupId)`로 `TB_REWARD.GetByGroup()` 에서 `RewardData[]`를 만든 뒤 적용

비책임(금지):
- `grantId` 멱등 처리
- 지급 기록(ledger) 저장/조회
- Firebase Functions/Firestore 호출
- pending queue/복구


---


## Dependencies (개념)

- InventoryManager — RewardManager는 Inventory에 "아이템/통화 추가(+) 적용"을 위임한다.
- SaveDataManager ↔ InventoryManager 직접 결합은 금지(상위 조립에서만 결합).


---


## Public API

- `ApplyRewardDatas(deltas)` — `RewardData[]`를 InventoryManager에 위임하여 적용
  ```csharp
  public void ApplyRewardDatas(RewardData[] deltas)
  {
      Singleton.Get<InventoryManager>().AddRewards(deltas);
  }
  ```
- `ApplyRewardGroupId(rewardGroupId)` — `rewardGroupId`를 RewardData[]로 변환 후 적용 (기존 API 유지)
- `ApplyRewardGroup(rewardGroupId)` — `CommonResult<RewardApplyResult>` 반환
  - `RewardApplyResult.AppliedRewards`로 이번 호출에서 실제 적용한 `RewardData[]`를 조회할 수 있다.
  - `rewardGroupId`가 비어 있으면 성공 + 빈 배열(`AppliedRewards=[]`) 반환


---


## 컨텐츠 테이블 통합 (TB_REWARD 직접 참조)

- `ResolveRewardDeltas(rewardGroupId) -> RewardData[]`
  - `TB_REWARD.GetByGroup(rewardGroupId)` 로 보상 그룹의 행 리스트를 조회하여 `RewardData[]`를 생성한다.
  - 각 행의 `{ Type, Id, Amount }` → `RewardData` 변환. empty Id / amount <= 0 행은 skip.
  - 원격 호출/네트워크 금지. 테이블 조회만 허용.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/03-ssot](../../../03-ssot/SKILL.md) §UPM Packages Sync

- RewardManager:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Reward/RewardManager.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Reward/RewardManager.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Reward/RewardManager.cs`
- RewardData:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Reward/RewardData.cs`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Reward/RewardData.cs`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Reward/RewardData.cs`

asmdef:
- `Devian.Samples.MobileSystem.asmdef`
- 참조: `Devian.Domain.Game` (TB_REWARD 테이블), `Devian.Domain.Common` (CommonResult)


---


## Sequence Examples


### A) Purchase

1) PurchaseManager → 서버 `verifyPurchase`
2) 응답 `resultStatus == GRANTED`
3) PurchaseManager: `ResolveRewardGroupId(internalProductId)` → `rewardGroupId`
4) PurchaseManager → `Singleton.Get<RewardManager>().ApplyRewardGroup(rewardGroupId)` (구매 결과 payload용 `AppliedRewards` 확보)
5) RewardManager: `ResolveRewardDeltas(rewardGroupId)` → `RewardData[]` → `ApplyRewardDatas(deltas)`
6) UI/표시는 서버 `entitlementsSnapshot` 기준으로 갱신


### B) Mission

1) MissionManager가 로컬 ledger에서 `grantId` 지급 여부 확인
2) 미지급이면 ledger를 `pending`으로 기록
3) MissionManager → `Singleton.Get<RewardManager>().ApplyRewardGroupId(rewardGroupId)`
4) RewardManager: `ResolveRewardDeltas(rewardGroupId)` → RewardData[] → `ApplyRewardDatas(deltas)`
5) 성공 시 ledger를 `granted`로 확정, 실패 시 `pending` 유지(재시도)


---


## Related

- [49-reward-system/03-ssot](../03-ssot/SKILL.md) — RewardData 스키마 정본
- [93-game-inventory-system/10-inventory-manager](../../93-game-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (AddRewards 위임 대상)
- [30-purchase-system/30-samples-purchase-manager](../../30-purchase-system/30-samples-purchase-manager/SKILL.md) — PurchaseManager (지급 요청 원점)
