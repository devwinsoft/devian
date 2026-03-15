# 03-ssot — 51-treasure-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 Treasure 시스템의 정본이다.

- `TREASURE_GRADE_TYPE` enum
- `TREASURE_CHEST`, `TREASURE_PROGRESS`, `TREASURE_GROUP` 테이블 스키마
- `TreasureStorage` 상태 모델
- `CollectChest` / `CollectProgress` 동작 규칙
- TreasureManager / RewardManager 경계

`RewardData`와 `rewardGroupId` 해석 정본은 [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)다.
이 문서는 Treasure 전용 상태와 collect 규칙만 정의한다.

---

## A) Core Terms

- `gradeType`: chest grade key (`TREASURE_GRADE_TYPE`)
- `treasureGroupId`: Treasure 시스템 내부 fan-out group key
- `rewardGroupId`: Reward 시스템 지급 group key
- `Progress`: `TreasureStorageProgress` 하위 객체 (exp/level 묶음)
- `currentExp`: 현재 progress exp (`Progress.CurrentExp`)
- `currentLevel`: 현재 progress reward level (`Progress.CurrentLevel`)
- `maxLevel`: `TREASURE_PROGRESS.level`의 최대값

---

## B) Enum Source

`TREASURE_GRADE_TYPE`의 입력 정본:

- `input/Domains/Game/ENUM_META.json`

값:

- `NONE`
- `COMMON`
- `RARE`
- `EPIC`
- `LEGENDARY`
- `MYTHIC`

규칙:

- `UNCOMMON`은 사용하지 않는다.
- chest count key는 `NONE`을 사용하지 않는다.

---

## C) Table Schema

파일:

- `input/Domains/Game/TreasureTable.xlsx`

### C-1) `TREASURE_CHEST`

| field | type | note |
|------|------|------|
| `treasureGradeType` | `TREASURE_GRADE_TYPE` (pk) | chest grade key |
| `treasureGroupId` | string | `TREASURE_GROUP.treasureGroupId` FK |

규칙:

- grade별 collect entry는 1행만 존재한다.
- `treasureGroupId`는 비어 있지 않아야 한다.

### C-2) `TREASURE_PROGRESS`

| field | type | note |
|------|------|------|
| `level` | int (pk) | progress reward level |
| `treasureGradeType` | `TREASURE_GRADE_TYPE` | level reward grade metadata |
| `maxExp` | int | collect 필요 exp |
| `treasureGroupId` | string | `TREASURE_GROUP.treasureGroupId` FK |

규칙:

- `level`은 1-base 연속 값 사용을 권장한다.
- `maxExp > 0`
- `treasureGroupId`는 비어 있지 않아야 한다.

### C-3) `TREASURE_GROUP`

| field | type | note |
|------|------|------|
| `index` | int (pk) | row key |
| `treasureGroupId` | string (`group:true`) | treasure reward fan-out group |
| `rewardGroupId` | string | RewardManager apply key |

규칙:

- 같은 `treasureGroupId` 아래 여러 row는 모두 적용 대상이다.
- `rewardGroupId`는 비어 있지 않아야 한다.

### C-4) Container expectation

코드젠 target:

- `TB_TREASURE_CHEST.Get(gradeType)`
- `TB_TREASURE_PROGRESS.Get(level)`
- `TB_TREASURE_PROGRESS.GetAll()`
- `TB_TREASURE_GROUP.GetByGroup(treasureGroupId)`

codegen 완료 상태. generated field name: `TreasureGradeType` (PascalCase).

---

## D) TreasureStorage State

정본 모델:

```csharp
public sealed class TreasureStorageProgress
{
    public int CurrentExp { get; set; }
    public int CurrentLevel { get; set; } = 1;
}

public sealed class TreasureStorage
{
    public int SchemaVersion { get; set; }
    public Dictionary<TREASURE_GRADE_TYPE, int> ChestCounts { get; }
    public TreasureStorageProgress Progress { get; }
}
```

기본값:

- `SchemaVersion = 1`
- `Progress.CurrentExp = 0`
- `Progress.CurrentLevel = 1`
- 모든 chest count 기본값은 0

---

## E) `CollectChest` Rules

입력:

- `gradeType: TREASURE_GRADE_TYPE`

처리:

1. `TreasureStorage.ChestCounts[gradeType]`를 읽는다.
2. count가 0 이하이면 valid no-op으로 종료한다.
3. `TB_TREASURE_CHEST.Get(gradeType)`로 row를 찾는다.
4. row의 `treasureGroupId`로 `TB_TREASURE_GROUP.GetByGroup(...)`를 조회한다.
5. `count`회 반복하면서 각 group row의 `rewardGroupId`를 순서대로 `RewardManager.ApplyRewardGroup(...)`에 전달한다.
6. 모든 지급이 성공하면 해당 grade count를 0으로 갱신한다.

실패:

- row 누락 / group row 없음 / 빈 `rewardGroupId` / `RewardManager` 실패 시 `CommonResult.Failure`
- 실패 시 chest count는 변경되지 않는다.

---

## F) `CollectProgress` Rules

처리:

1. `currentLevel`로 `TB_TREASURE_PROGRESS.Get(currentLevel)`을 조회한다.
2. row가 없으면 실패한다.
3. `currentExp < row.maxExp`이면 valid no-op으로 종료한다.
4. row의 `treasureGroupId`로 `TB_TREASURE_GROUP.GetByGroup(...)`를 조회한다.
5. 각 group row의 `rewardGroupId`를 순서대로 `RewardManager.ApplyRewardGroup(...)`에 전달한다.
6. 모든 지급이 성공하면 `currentExp -= row.maxExp`를 적용한다.
7. `currentLevel++` 후 `currentLevel > maxLevel`이면 `1`로 wrap한다.

규칙:

- 한 번의 `CollectProgress()` 호출은 현재 level 보상 1회만 처리한다.
- 남은 exp가 다음 level `maxExp` 이상이어도 추가 collect는 다음 호출에서 수행한다.

실패:

- row 누락 / group row 없음 / 빈 `rewardGroupId` / `RewardManager` 실패 시 `CommonResult.Failure`
- 실패 시 `currentExp`, `currentLevel`은 변경되지 않는다.

---

## G) TreasureGroup Apply Semantics

- `TREASURE_GROUP`은 `treasureGroupId -> rewardGroupId[]` fan-out 테이블이다.
- 하나의 collect는 group 아래의 모든 `rewardGroupId`를 적용한다.
- 개별 `rewardGroupId` 내부의 랜덤 선택/`REWARD` 해석은 RewardManager 정본을 따른다.

---

## H) Runtime Ownership

- TreasureManager: table lookup + collect orchestration + storage mutation
- TreasureStorage: chest/progress 상태 저장
- RewardManager: `rewardGroupId` 지급 실행
- InventoryManager: concrete inventory mutation

---

## I) Known Current-State Gaps

현재 알려진 gap 없음.

---

## J) SaveData Integration

- root JSON key: `"treasure"`
- section codec: `SaveDataJsonCodecTreasure`
- version gate: `TreasureVersion` (= 20, `CurrentVersion`과 동일)
- serialize: `TreasureStorage` → `JObject`
- deserialize: `JObject` → `TreasureStorage`
- progress 상태는 `"progress"` 하위 객체로 직렬화한다 (`currentExp`, `currentLevel`)
- deserialize 시 `"progress"` 키가 없으면 root flat `currentExp`/`currentLevel`로 backward compat fallback
- `ChestCounts` dictionary key: enum name string (예: `"COMMON"`, `"EPIC"`), [11-treasure-storage](../11-treasure-storage/SKILL.md) §Target Save Shape 준수
- `SavePayloadSummary`에 `TreasureChestTotal`, `TreasureLevel` 추가

---

## Related

- [10-treasure-manager](../10-treasure-manager/SKILL.md)
- [11-treasure-storage](../11-treasure-storage/SKILL.md)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
