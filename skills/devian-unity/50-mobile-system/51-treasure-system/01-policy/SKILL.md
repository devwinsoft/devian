# 51-treasure-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

Treasure 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) TreasureManager가 TreasureStorage를 단독 소유한다

- `TreasureStorage`는 `TreasureManager` 내부 상태다.
- 외부 시스템은 storage를 직접 mutate하지 않고 `TreasureManager` 경유로만 접근한다.

### 2) chest/progress reward 실행은 RewardManager 단일 경로를 사용한다

- TreasureManager는 `rewardGroupId`를 직접 해석하지 않는다.
- `TREASURE_GROUP.rewardGroupId`마다 `RewardManager.ApplyRewardGroup(...)`를 호출한다.
- inventory 직접 수정 금지.

### 3) 콘텐츠 소스는 `TREASURE_CHEST`, `TREASURE_PROGRESS`, `TREASURE_GROUP` 세 테이블이다

- `TREASURE_CHEST`: 등급별 chest collect entry
- `TREASURE_PROGRESS`: level별 progress collect entry
- `TREASURE_GROUP`: treasureGroupId -> rewardGroupId[] fan-out
- 호출자(Shop, Mission, Ads 등)는 위 테이블을 직접 파싱하지 않는다.

### 4) `TREASURE_GRADE_TYPE`은 Generated enum이며 chest 등급 키다

- 입력 정본: `input/Domains/Game/ENUM_META.json`
- enum 값: `NONE`, `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `MYTHIC`
- `UNCOMMON`은 사용하지 않는다.

### 5) `CollectChest(gradeType)`는 소비형 수집이다

- `TreasureStorage`의 해당 grade chest count를 기준으로 반복 지급한다.
- 성공 시 해당 grade count는 0이 된다.
- count가 0이면 valid no-op으로 처리한다.

### 6) `CollectProgress()`는 현재 level 1회 수집만 담당한다

- `Progress.CurrentLevel` row의 `maxExp`를 기준으로 claim 가능 여부를 판단한다.
- 성공 시 `Progress.CurrentExp -= maxExp`, `Progress.CurrentLevel++`를 적용한다.
- 증가 후 `Progress.CurrentLevel > maxLevel`이면 `1`로 wrap한다.
- 한 번의 호출은 현재 level 보상 1회만 지급한다. 남은 exp가 충분해도 다음 level 보상은 다음 호출에서 처리한다.

### 7) collect는 all-or-nothing 상태 변경을 유지한다

- collect 중 하나라도 `RewardManager` 실패가 발생하면 storage 상태는 호출 전과 동일해야 한다.
- chest count 차감, exp 차감, level 변경은 reward apply 성공 후에만 커밋한다.

### 8) 빈 row / 누락 row는 안전 실패한다

- `TREASURE_CHEST`, `TREASURE_PROGRESS`, `TREASURE_GROUP` row 누락은 `CommonResult.Failure`로 정리한다.
- 빈 `treasureGroupId`, 빈 `rewardGroupId`는 invalid다.

---

## Client API (target)

`TreasureManager`
- `TreasureStorage Storage { get; }`
- `CollectChest(TREASURE_GRADE_TYPE gradeType)` -> `CommonResult`
- `CollectProgress()` -> `CommonResult`
