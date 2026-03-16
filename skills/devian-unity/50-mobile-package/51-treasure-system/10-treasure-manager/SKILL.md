# 10-treasure-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `TreasureManager` 설계 문서다.
이 매니저는 `InventoryManager.Instance.Storage`의 treasure 필드를 사용하여 chest collect를 orchestration 한다.

---

## Implementation Location (target 3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Treasure/TreasureManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Treasure/TreasureManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Treasure/TreasureManager.cs`

---

## Target Class Design

```csharp
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class TreasureManager : CompoSingleton<TreasureManager>
    {
        InventoryStorage storage => InventoryManager.Instance.Storage;

        public CommonResult OpenCollectedChests(TREASURE_GRADE_TYPE gradeType) { ... }
        public CommonResult OpenCurrentChest() { ... }
    }
}
```

---

## Responsibilities

- `InventoryStorage` treasure 필드 접근 (`InventoryManager.Instance.Storage`)
- `OpenCollectedChests(TREASURE_GRADE_TYPE)` 구현
- `OpenCurrentChest()` 구현
- `TREASURE_CHEST`, `TREASURE_REWARD` lookup
- `TREASURE_REWARD` 조건 필터링 (`selectBestRewardRow`)
- `RewardManager.ApplyRewardGroup(...)` 호출 orchestration
- collect 성공 시 storage 상태 커밋

비책임:

- inventory 직접 수정
- `rewardGroupId` 내부 해석
- ledger/멱등/복구
- UI 연출

---

## Dependencies

- `InventoryManager` / `InventoryStorage`
- `TB_TREASURE_CHEST`
- `TB_TREASURE_REWARD`
- `GameMessageManager` (`GetStat()`)
- `GameMessageRule` (`IsConditionSatisfied()`)
- `RewardManager`
- `Devian.Domain.Common`
- `Devian.Domain.Game`

---

## Public API

- `OpenCollectedChests(TREASURE_GRADE_TYPE gradeType) -> CommonResult`
- `OpenCurrentChest() -> CommonResult`

---

## Condition Selection (`selectBestRewardRow`)

`TREASURE_REWARD.GetByGroup(gradeType)` 결과 중 조건에 부합하는 row를 선택한다.

1. `conditionMsgId`가 비어있으면 → 조건 통과 (조건 자체가 없음)
2. `conditionMsgId`가 있고 `ConditionValue`가 null이면 → **무조건 실패**
3. `conditionMsgId`가 있고 `ConditionValue`가 있으면 → `GameMessageManager.Instance.GetStat(conditionMsgId)` 값을 `GameMessageRule.IsConditionSatisfied(stat, conditionOp, conditionValue)` 로 비교
4. 조건 통과한 row 중 `Level`이 가장 높은 row **1개**를 선택한다
5. 조건 통과 row가 없으면 → 실패 (`TREASURE_REWARD_EMPTY`)

---

## `OpenCollectedChests` Flow

1. `gradeType` 검증 (`NONE` 금지)
2. `InventoryStorage`에서 treasure count 조회
3. count가 0이면 `CommonResult.Ok()` 반환
4. `TB_TREASURE_REWARD.GetByGroup(gradeType)` 조회
5. `selectBestRewardRow`로 조건 충족 최고 레벨 row 1개 선택
6. `count`회 반복하며 best row의 `rewardGroupId`를 `RewardManager.ApplyRewardGroup(...)`에 전달
7. 모두 성공하면 treasure count를 0으로 커밋

---

## `OpenCurrentChest` Flow

1. `InventoryStorage`의 `TreasureCurrent.Level` 조회
2. `TB_TREASURE_CHEST.Get(level)` 조회
3. row가 없으면 실패
4. `TreasureCurrent.Exp < maxExp`이면 `CommonResult.Ok()` 반환
5. `chestRow.TreasureGradeType`로 `TB_TREASURE_REWARD.GetByGroup(...)` 조회
6. `selectBestRewardRow`로 조건 충족 최고 레벨 row 1개 선택
7. best row의 `rewardGroupId`를 `RewardManager.ApplyRewardGroup(...)`에 전달
8. 성공하면 `TreasureCurrent.Exp -= maxExp`
9. `TreasureCurrent.Level++`
10. `TreasureCurrent.Level > maxLevel`이면 `1`로 wrap

---

## Hard Rules

- collect는 all-or-nothing storage mutation을 유지한다.
- `OpenCollectedChests`는 해당 grade의 전체 treasure count를 한 번에 수집한다.
- `OpenCurrentChest`는 현재 level 보상 1회만 처리한다.
- collect 중 `RewardManager` 실패가 발생하면 InventoryStorage treasure 상태는 롤백된다.
- `TREASURE_REWARD` row 선택은 조건 필터 후 가장 높은 Level row 1개만 사용한다.
- `ConditionValue`가 null이면 무조건 조건 실패다. null 통과는 절대 허용하지 않는다.

---

## Implementation Plan

### 1) Enum / table codegen 정렬

- `TREASURE_TYPE`를 `TREASURE_GRADE_TYPE`로 교체한다.
- 값 집합을 `NONE`, `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `MYTHIC`로 고정한다.
- `TreasureTable.xlsx` 기반으로 `TB_TREASURE_CHEST`, `TB_TREASURE_REWARD`가 생성되도록 codegen을 재실행한다.

### 2) Runtime 모델 추가

- `Runtime/Treasure/` 폴더를 추가한다.
- `TreasureManager.cs`를 추가한다.
- `TreasureManager`는 `InventoryManager.Instance.Storage` 경유로 treasure 상태에 접근한다.

### 3) Chest collect 구현

- grade별 count 읽기/zero commit 로직을 구현한다.
- `TREASURE_REWARD` 조건 필터 후 best row 1개를 선택하여 지급한다.
- count가 큰 경우에도 순서와 atomicity가 유지되도록 한다.

### 4) Current chest collect 구현

- `currentLevel/currentExp` 기반 claim 가능 여부 판단을 구현한다.
- `TREASURE_REWARD` 조건 필터 후 best row 1개를 선택하여 지급한다.
- reward 적용 후 exp 차감, level 증가, max level wrap을 구현한다.
- 한 번 호출에 1 level만 수집하도록 고정한다.

### 5) SaveData 연동

- `SaveDataJsonCodecTreasure.cs` section codec을 추가한다.
- `SaveDataJsonCodec.cs` root codec에 treasure 섹션을 추가한다 (version 20).
- `SaveDataManager.cs`에 treasure sourcing을 연결한다.
- 관련 스킬 문서를 갱신한다: [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md).

### 6) Tests / verification

- enum 생성 확인 (`TREASURE_GRADE_TYPE`)
- chest collect: 0개 / 1개 / 다수 count (조건 필터 후 best row 선택)
- current chest collect: exp 부족 / 정확히 같음 / 초과 exp / max level wrap
- 조건 필터: conditionMsgId 비어있음 / ConditionValue null / 조건 미충족 / 조건 충족 best level
- 누락 row / 빈 reward / RewardManager 실패 시 상태 롤백

---

## Open Questions

해결 완료:

- ~~`treasuerGradeType` 오탈자~~ → 시트에서 `treasureGradeType`으로 수정 완료.
- ~~에러 코드~~ → `COMMON_ERROR_TYPE`에 `TREASURE_*` 에러 코드 추가. (`CommonTable.xlsx` `COMMON_ERROR` 시트)
- ~~save payload key~~ → root JSON `"treasure"` 키로 기존 save 구조에 병합. version 20.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
