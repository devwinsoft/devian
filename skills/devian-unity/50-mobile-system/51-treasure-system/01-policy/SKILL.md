# 51-treasure-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

Treasure 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) Treasure 상태는 InventoryStorage에 저장되며 TreasureManager가 mutation을 담당한다

- Treasure 필드(`TreasureCurrent`, `TreasureCounts`)는 `InventoryStorage` 내부에 있다.
- `TreasureManager`는 `InventoryManager.Instance.Storage` 경유로 treasure 상태를 mutate한다.
- 외부 시스템은 treasure 상태를 직접 mutate하지 않고 `TreasureManager` 경유로만 접근한다.

### 2) chest reward 실행은 RewardManager 단일 경로를 사용한다

- TreasureManager는 `rewardGroupId`를 직접 해석하지 않는다.
- 선택된 `TREASURE_REWARD` row의 `rewardGroupId`를 `RewardManager.ApplyRewardGroup(...)`에 전달한다.
- inventory 직접 수정 금지.

### 3) 콘텐츠 소스는 `TREASURE_CHEST`, `TREASURE_REWARD` 두 테이블이다

- `TREASURE_CHEST`: level별 chest collect entry
- `TREASURE_REWARD`: treasureGradeType -> 조건부 reward row[] (조건 필터 후 best level 1개 선택)
- 호출자(Shop, Mission, Ads 등)는 위 테이블을 직접 파싱하지 않는다.

### 4) `TREASURE_GRADE_TYPE`은 Generated enum이며 chest 등급 키다

- 입력 정본: `input/Domains/Game/ENUM_META.json`
- enum 값: `NONE`, `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `MYTHIC`
- `UNCOMMON`은 사용하지 않는다.

### 5) `OpenCollectedChests(gradeType)`는 소비형 수집이다

- `InventoryStorage`의 해당 grade treasure count를 기준으로 반복 지급한다.
- `TREASURE_REWARD` 조건 필터 후 best level row 1개의 `rewardGroupId`를 사용한다.
- 성공 시 해당 grade count는 0이 된다.
- count가 0이면 valid no-op으로 처리한다.

### 6) `OpenCurrentChest()`는 현재 level 1회 수집만 담당한다

- `TreasureCurrent.Level` row의 `maxExp`를 기준으로 claim 가능 여부를 판단한다.
- `TREASURE_REWARD` 조건 필터 후 best level row 1개의 `rewardGroupId`를 사용한다.
- 성공 시 `TreasureCurrent.Exp -= maxExp`, `TreasureCurrent.Level++`를 적용한다.
- 증가 후 `TreasureCurrent.Level > maxLevel`이면 `1`로 wrap한다.
- 한 번의 호출은 현재 level 보상 1회만 지급한다. 남은 exp가 충분해도 다음 level 보상은 다음 호출에서 처리한다.

### 7) collect는 all-or-nothing 상태 변경을 유지한다

- collect 중 하나라도 `RewardManager` 실패가 발생하면 storage 상태는 호출 전과 동일해야 한다.
- chest count 차감, exp 차감, level 변경은 reward apply 성공 후에만 커밋한다.

### 8) 빈 row / 누락 row는 안전 실패한다

- `TREASURE_CHEST`, `TREASURE_REWARD` row 누락은 `CommonResult.Failure`로 정리한다.
- 빈 `rewardGroupId`는 invalid다.
- 조건 충족 row가 없으면 `TREASURE_REWARD_EMPTY`로 실패한다.

### 9) `TREASURE_REWARD` 조건 선택 규칙

- `conditionMsgId`가 비어있으면 조건 통과 (조건 자체가 없음).
- `conditionMsgId`가 있고 `ConditionValue`가 null이면 **무조건 실패**. null 통과는 절대 불가.
- 조건 판정은 `GameMessageManager.GetStat()` + `GameMessageRule.IsConditionSatisfied()` 를 사용한다.
- 조건 통과 row 중 `Level`이 가장 높은 row 1개만 선택하여 지급한다.

---

## Client API (target)

`TreasureManager`
- `OpenCollectedChests(TREASURE_GRADE_TYPE gradeType)` -> `CommonResult`
- `OpenCurrentChest()` -> `CommonResult`
