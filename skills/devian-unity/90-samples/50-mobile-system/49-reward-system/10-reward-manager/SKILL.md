# 10-reward-manager


RewardManager는 입력된 `rewardId` 또는 **RewardData[]**를 로컬 인벤토리에 **적용(지급 실행)** 한다.

RewardManager는 **단일 concrete 클래스**이다.
- `rewardId -> RewardData[]` 변환은 `TB_REWARD` 테이블을 직접 참조하여 구현한다.
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
- `rewardId`를 입력받아 `ResolveRewardDeltas(rewardId)`로 `TB_REWARD` 테이블에서 `RewardData[]`를 만든 뒤 적용

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
- `ApplyRewardId(rewardId)` — `rewardId`를 RewardData[]로 변환 후 적용


---


## 컨텐츠 테이블 통합 (TB_REWARD 직접 참조)

- `ResolveRewardDeltas(rewardId) -> RewardData[]`
  - `TB_REWARD` 테이블에서 `rewardId`에 해당하는 보상 목록을 조회하여 `RewardData[]`를 생성한다.
  - 원격 호출/네트워크 금지. 테이블 조회만 허용.


---


## Implementation Location (SSOT)

- RewardManager:
  - UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardManager.cs`
  - Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardManager.cs`
  - Assets: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/RewardManager/RewardManager.cs`
- RewardData:
  - UPM: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardData.cs`
  - Packages: `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/RewardManager/RewardData.cs`
  - Assets: `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/0.1.0/MobileSystem/Runtime/RewardManager/RewardData.cs`
- 미러링/정본 정책은 상위 정책 [devian-unity/01-policy](../../../../01-policy/SKILL.md)를 따른다.

asmdef:
- `Devian.Samples.MobileSystem.asmdef`
- 참조: `Devian.Domain.Game` (TB_REWARD 테이블), `Devian.Domain.Common` (CommonResult)


---


## Sequence Examples


### A) Purchase

1) PurchaseManager → 서버 `verifyPurchase`
2) 응답 `resultStatus == GRANTED`
3) PurchaseManager: `ResolveRewardId(internalProductId)` → `rewardId`
4) PurchaseManager → `Singleton.Get<RewardManager>().ApplyRewardId(rewardId)`
5) RewardManager: `ResolveRewardDeltas(rewardId)` → `RewardData[]` → `ApplyRewardDatas(deltas)`
6) UI/표시는 서버 `entitlementsSnapshot` 기준으로 갱신


### B) Mission

1) MissionManager가 로컬 ledger에서 `grantId` 지급 여부 확인
2) 미지급이면 ledger를 `pending`으로 기록
3) MissionManager → `Singleton.Get<RewardManager>().ApplyRewardId(rewardId)`
4) RewardManager: `ResolveRewardDeltas(rewardId)` → RewardData[] → `ApplyRewardDatas(deltas)`
5) 성공 시 ledger를 `granted`로 확정, 실패 시 `pending` 유지(재시도)


---


## Related

- [49-reward-system/03-ssot](../03-ssot/SKILL.md) — RewardData 스키마 정본
- [52-inventory-system/10-inventory-manager](../../52-inventory-system/10-inventory-manager/SKILL.md) — InventoryManager (AddRewards 위임 대상)
- [30-purchase-system/30-samples-purchase-manager](../../30-purchase-system/30-samples-purchase-manager/SKILL.md) — PurchaseManager (지급 요청 원점)
