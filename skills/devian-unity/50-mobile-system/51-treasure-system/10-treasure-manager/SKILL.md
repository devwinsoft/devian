# 10-treasure-manager

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 `TreasureManager` 설계 문서다.
이 매니저는 `TreasureStorage`를 소유하고 chest/progress collect를 orchestration 한다.

---

## Implementation Location (target 3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/Treasure/TreasureManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Treasure/TreasureManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Treasure/TreasureManager.cs`

---

## Target Class Design

```csharp
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class TreasureManager : CompoSingleton<TreasureManager>
    {
        readonly TreasureStorage _storage = new();
        public TreasureStorage Storage => _storage;

        public CommonResult CollectChest(TREASURE_GRADE_TYPE gradeType) { ... }
        public CommonResult CollectProgress() { ... }
    }
}
```

---

## Responsibilities

- `TreasureStorage` 소유
- `CollectChest(TREASURE_GRADE_TYPE)` 구현
- `CollectProgress()` 구현
- `TREASURE_CHEST`, `TREASURE_PROGRESS`, `TREASURE_GROUP` lookup
- `RewardManager.ApplyRewardGroup(...)` 호출 orchestration
- collect 성공 시 storage 상태 커밋

비책임:

- inventory 직접 수정
- `rewardGroupId` 내부 해석
- ledger/멱등/복구
- UI 연출

---

## Dependencies

- `TreasureStorage`
- `TB_TREASURE_CHEST`
- `TB_TREASURE_PROGRESS`
- `TB_TREASURE_GROUP`
- `RewardManager`
- `Devian.Domain.Common`
- `Devian.Domain.Game`

---

## Public API

- `Storage`
- `CollectChest(TREASURE_GRADE_TYPE gradeType) -> CommonResult`
- `CollectProgress() -> CommonResult`

---

## `CollectChest` Flow

1. `gradeType` 검증 (`NONE` 금지)
2. storage에서 chest count 조회
3. count가 0이면 `CommonResult.Ok()` 반환
4. `TB_TREASURE_CHEST.Get(gradeType)` 조회
5. `treasureGroupId`로 `TB_TREASURE_GROUP.GetByGroup(...)` 조회
6. `count`회 반복하며 각 `rewardGroupId`를 `RewardManager.ApplyRewardGroup(...)`에 전달
7. 모두 성공하면 chest count를 0으로 커밋

---

## `CollectProgress` Flow

1. storage의 `Progress.CurrentLevel` 조회
2. `TB_TREASURE_PROGRESS.Get(currentLevel)` 조회
3. row가 없으면 실패
4. `currentExp < maxExp`이면 `CommonResult.Ok()` 반환
5. `treasureGroupId`로 `TB_TREASURE_GROUP.GetByGroup(...)` 조회
6. 각 `rewardGroupId`를 `RewardManager.ApplyRewardGroup(...)`에 전달
7. 모두 성공하면 `Progress.CurrentExp -= maxExp`
8. `Progress.CurrentLevel++`
9. `Progress.CurrentLevel > maxLevel`이면 `1`로 wrap

---

## Hard Rules

- collect는 all-or-nothing storage mutation을 유지한다.
- `CollectChest`는 해당 grade의 전체 count를 한 번에 수집한다.
- `CollectProgress`는 현재 level 보상 1회만 처리한다.
- collect 중 `RewardManager` 실패가 발생하면 storage 상태는 롤백된다.

---

## Implementation Plan

### 1) Enum / table codegen 정렬

- `TREASURE_TYPE`를 `TREASURE_GRADE_TYPE`로 교체한다.
- 값 집합을 `NONE`, `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `MYTHIC`로 고정한다.
- `TreasureTable.xlsx` 기반으로 `TB_TREASURE_CHEST`, `TB_TREASURE_PROGRESS`, `TB_TREASURE_GROUP`가 생성되도록 codegen을 재실행한다.

### 2) Runtime 모델 추가

- `Runtime/Treasure/` 폴더를 추가한다.
- `TreasureStorage.cs`와 `TreasureManager.cs`를 추가한다.
- `TreasureManager`에 `_storage`와 `Storage` property를 추가한다.

### 3) Chest collect 구현

- grade별 count 읽기/zero commit 로직을 구현한다.
- `TREASURE_CHEST -> TREASURE_GROUP -> RewardManager` fan-out을 구현한다.
- count가 큰 경우에도 순서와 atomicity가 유지되도록 한다.

### 4) Progress collect 구현

- `currentLevel/currentExp` 기반 claim 가능 여부 판단을 구현한다.
- reward 적용 후 exp 차감, level 증가, max level wrap을 구현한다.
- 한 번 호출에 1 level만 수집하도록 고정한다.

### 5) SaveData 연동

- `SaveDataJsonCodecTreasure.cs` section codec을 추가한다.
- `SaveDataJsonCodec.cs` root codec에 treasure 섹션을 추가한다 (version 20).
- `SaveDataManager.cs`에 treasure sourcing을 연결한다.
- 관련 스킬 문서를 갱신한다: [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md).

### 6) Tests / verification

- enum 생성 확인 (`TREASURE_GRADE_TYPE`)
- chest collect: 0개 / 1개 / 다수 count
- progress collect: exp 부족 / 정확히 같음 / 초과 exp / max level wrap
- 누락 row / 빈 group / RewardManager 실패 시 상태 롤백

---

## Open Questions

해결 완료:

- ~~`treasuerGradeType` 오탈자~~ → 시트에서 `treasureGradeType`으로 수정 완료.
- ~~에러 코드~~ → `COMMON_ERROR_TYPE`에 `TREASURE_*` 에러 코드 추가. (`CommonTable.xlsx` `COMMON_ERROR` 시트)
- ~~save payload key~~ → root JSON `"treasure"` 키로 기존 save 구조에 병합. version 20.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-treasure-storage](../11-treasure-storage/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
