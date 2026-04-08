# 03-ssot — 51-treasure-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 Treasure 시스템의 정본이다.

- `ITEM_GRADE_TYPE` enum
- `TREASURE_CHEST`, `TREASURE_REWARD` 테이블 스키마
- `InventoryStorage` 내 Treasure 상태 모델 (`TreasureCurrent`, `TreasureCounts`)
- `OpenCollectedChests` / `OpenCurrentChest` 동작 규칙
- TreasureManager / RewardManager 경계

`RewardData`와 `reward_group_id` 해석 정본은 [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)다.
이 문서는 Treasure 전용 상태와 collect 규칙만 정의한다.

---

## A) Core Terms

- `gradeType`: chest/reward grade key (`ITEM_GRADE_TYPE`)
- `reward_group_id`: Reward 시스템 지급 group key
- `TreasureCurrent`: `InventoryTreasureCurrent` 하위 객체 (exp/level 묶음, `InventoryStorage` 소속)
- `exp`: 현재 treasure exp (`TreasureTreasureCurrent.Exp`)
- `level`: 현재 treasure reward level (`TreasureTreasureCurrent.Level`)
- `maxLevel`: `TREASURE_CHEST.level`의 최대값

---

## B) Enum Source

`ITEM_GRADE_TYPE`의 입력 정본:

- `input/Domains/Game/ENUM_ITEM.json`

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
| `level` | int (pk) | chest reward level |
| `treasure_grade_type` | `ITEM_GRADE_TYPE` | level reward grade metadata |
| `max_exp` | int | collect 필요 exp |

규칙:

- `level`은 1-base 연속 값 사용을 권장한다.
- `max_exp > 0`

### C-2) `TREASURE_REWARD`

| field | type | note |
|------|------|------|
| `index` | int (pk) | row key |
| `treasure_grade_type` | `ITEM_GRADE_TYPE` (`group:true`) | grade별 reward fan-out group key |
| `level` | int | group 내 level |
| `condition_msg_id` | string | 조건 메시지 ID |
| `condition_op` | `GAME_MESSAGE_OP_TYPE` | 조건 연산자 |
| `condition_value` | `CBigInt` | 조건 목표값 |
| `reward_group_id` | string | RewardManager apply key |

규칙:

- 같은 `treasure_grade_type` 아래 여러 row는 모두 적용 대상이다.
- `reward_group_id`는 비어 있지 않아야 한다.

### C-3) Container expectation

코드젠 target:

- `TB_TREASURE_CHEST.Get(level)`
- `TB_TREASURE_CHEST.GetAll()`
- `TB_TREASURE_REWARD.GetByGroup(ITEM_GRADE_TYPE)`

codegen 완료 상태. generated field name: `Treasure_grade_type` (PascalCase).

---

## D) Treasure State (in InventoryStorage)

Treasure 상태는 `InventoryStorage` 내부에 포함된다. 별도 `TreasureStorage` 클래스는 없다.

정본 모델:

```csharp
public sealed class InventoryTreasureCurrent
{
    public int Exp { get; set; }
    public int Level { get; set; } = 1;
}

// InventoryStorage 내부:
//   InventoryTreasureCurrent TreasureCurrent { get; }
//   Dictionary<ITEM_GRADE_TYPE, int> TreasureCounts { get; }
//   GetTreasureCount / AddTreasure / SetTreasureCount / AddTreasureExp / ResetTreasure
```

기본값:

- `TreasureTreasureCurrent.Exp = 0`
- `TreasureTreasureCurrent.Level = 1`
- 모든 treasure count 기본값은 0

---

## E) Condition Selection Rules (`selectBestRewardRow`)

`TREASURE_REWARD.GetByGroup(gradeType)` 결과 중 조건에 부합하는 row 1개를 선택한다.

1. `condition_msg_id`가 비어있으면 → 조건 통과 (조건 자체가 없음)
2. `condition_msg_id`가 있고 `Condition_value`가 null이면 → **무조건 실패** (null 통과 절대 불가)
3. `condition_msg_id`가 있고 `Condition_value`가 있으면 → `GameMessageManager.Instance.GetStat(condition_msg_id)` 값을 `GameMessageRule.IsConditionSatisfied(stat, condition_op, condition_value)` 로 비교
4. 조건 통과한 row 중 `Level`이 가장 높은 row **1개**를 선택한다
5. 조건 통과 row가 없으면 → 실패 (`TREASURE_REWARD_EMPTY`)

---

## F) `OpenCollectedChests` Rules

입력:

- `gradeType: ITEM_GRADE_TYPE`

처리:

1. `InventoryStorage.TreasureCounts[gradeType]`를 읽는다.
2. count가 0 이하이면 valid no-op으로 종료한다.
3. `TB_TREASURE_REWARD.GetByGroup(gradeType)`로 reward rows를 조회한다.
4. `selectBestRewardRow`로 조건 충족 최고 레벨 row 1개를 선택한다.
5. `count`회 반복하면서 best row의 `reward_group_id`를 `RewardManager.ApplyRewardGroup(...)`에 전달한다.
6. 모든 지급이 성공하면 해당 grade count를 0으로 갱신한다.

실패:

- reward row 없음 / 조건 충족 row 없음 / 빈 `reward_group_id` / `RewardManager` 실패 시 `CommonResult.Failure`
- 실패 시 chest count는 변경되지 않는다.

---

## G) `OpenCurrentChest` Rules

처리:

1. `TreasureCurrent.Level`로 `TB_TREASURE_CHEST.Get(level)`을 조회한다.
2. row가 없으면 실패한다.
3. `TreasureCurrent.Exp < row.max_exp`이면 valid no-op으로 종료한다.
4. row의 `treasure_grade_type`로 `TB_TREASURE_REWARD.GetByGroup(...)`를 조회한다.
5. `selectBestRewardRow`로 조건 충족 최고 레벨 row 1개를 선택한다.
6. best row의 `reward_group_id`를 `RewardManager.ApplyRewardGroup(...)`에 전달한다.
7. 성공하면 `TreasureCurrent.Exp -= row.max_exp`를 적용한다.
8. `TreasureCurrent.Level++` 후 `TreasureCurrent.Level > maxLevel`이면 `1`로 wrap한다.

규칙:

- 한 번의 `OpenCurrentChest()` 호출은 현재 level 보상 1회만 처리한다.
- 남은 exp가 다음 level `max_exp` 이상이어도 추가 collect는 다음 호출에서 수행한다.

실패:

- row 누락 / 조건 충족 row 없음 / 빈 `reward_group_id` / `RewardManager` 실패 시 `CommonResult.Failure`
- 실패 시 `TreasureCurrent.Exp`, `TreasureCurrent.Level`은 변경되지 않는다.

---

## H) TreasureReward Apply Semantics

- `TREASURE_REWARD`는 `treasure_grade_type -> row[]` 테이블이다.
- 각 row는 `condition_msg_id/condition_op/condition_value` 조건을 가진다.
- 하나의 collect는 조건을 통과한 row 중 가장 높은 `Level` row 1개의 `reward_group_id`만 적용한다.
- 개별 `reward_group_id` 내부의 랜덤 선택/`REWARD` 해석은 RewardManager 정본을 따른다.

---

## I) Runtime Ownership

- TreasureManager: table lookup + collect orchestration + InventoryStorage treasure mutation
- InventoryStorage: treasure 상태 저장 (`TreasureCurrent`, `TreasureCounts`)
- RewardManager: `reward_group_id` 지급 실행
- InventoryManager: concrete inventory mutation (treasure 포함)

---

## J) Known Current-State Gaps

현재 알려진 gap 없음.

---

## K) SaveData Integration

- root JSON key: `"treasure"`
- section codec: `SaveDataJsonCodecTreasure`
- version gate: `TreasureVersion` (= 20, `CurrentVersion`과 동일)
- serialize: `InventoryStorage` → treasure `JObject`
- deserialize: treasure `JObject` → `InventoryStorage` treasure fields
- current 상태는 `"current"` 하위 객체로 직렬화한다 (`exp`, `level`)
- `TreasureCounts` dictionary key: enum name string (예: `"COMMON"`, `"EPIC"`)
- `SavePayloadSummary`에 `TreasureChestTotal`, `TreasureLevel` 추가

---

## Related

- [10-treasure-manager](../10-treasure-manager/SKILL.md)
- [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)
