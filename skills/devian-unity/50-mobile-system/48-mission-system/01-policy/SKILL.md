# 48-mission-system — Policy

Status: ACTIVE  
AppliesTo: v10  
Type: Policy / Entry Point

## Purpose

Mission(일일/업적) 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) 미션 조건 정본은 `MISSION_STAT`이다

- `MISSION_DAY`, `MISSION_ACHIEVE`는 `missionStatId`만 가진다.
- 조건 타입/연산자 정본은 `MISSION_STAT(missionStatId, statType, opType)`다.
- MissionManager/Scheduler는 `missionStatId -> MISSION_STAT`를 resolve해서 runtime 바인딩을 만든다.

### 2) 진행도 입력은 `MissionTriggerSystem`으로만 받는다

- MissionManager는 `MessageSystem<int, MISSION_STAT_TYPE>` 특화 인스턴스인 `MissionTriggerSystem`을 소유한다.
- 다른 시스템은 `MissionManager.Notify(statType, value)`만 호출한다.
- MissionRuntime이 별도 gameplay API를 직접 호출받는 구조는 금지한다.

### 3) trigger 처리 순서는 `stats 갱신 -> runtime notify`다

- MissionManager.Notify 내부에서 같은 `statType`을 참조하는 모든 `MISSION_STAT` row를 순회하며 `stats[missionStatId]`를 먼저 갱신한다.
  - `SUM`: `current + delta`
  - `MAX`: `max(current, delta)`
- 그 다음 내부 `MissionTriggerSystem`으로 runtime 구독자 notify를 수행한다.

### 4) 진행도 저장 정본은 mission type별로 분리한다

- `DAY`: runtime 인스턴스가 `progressValue`를 자체 보유/저장한다.
- `ACHIEVE`: 진행도 정본은 `MissionStorage.stats[string missionStatId]`다.
- `ACHIEVE` runtime의 `progressValue`는 `stats`를 읽어 반영하는 projection이다.

### 5) `ACHIEVE` level up은 반드시 재구독 전환을 수행한다

- 다음 level row가 있으면 같은 runtime(`missionUid` 유지)을 mutation한다.
- 순서:
  1. 기존 statType 구독 해지
  2. `level` 갱신
  3. `missionStatId/statType/opType/conditionValue`를 다음 row 기준으로 교체
  4. 새 `missionStatId` reader(`stats[missionStatId]`) 연결
  5. 새 statType으로 재구독
- `LevelUp`에서 progress를 수동으로 set 하지 않는다. source of truth는 `stats`다.

### 6) `ACHIEVE`의 `periodKey`는 저장하지 않는다

- `ACHIEVE` period는 고정 `"once"`다.
- save payload에서 `ACHIEVE.periodKey`는 제외하고, deserialize 시 `"once"`를 주입한다.

### 7) legacy 호환 로직은 두지 않는다

- mission schema 기본값은 v2다.
- 구 포맷 fallback(`missionKind`, `ACHIEVE progressValue` 복원, legacy seed)은 사용하지 않는다.

### 8) 저장/복구 및 보상 처리 경계

- MissionManager가 claim orchestration을 담당하고, 보상 적용은 RewardManager에 위임한다.
- claim 성공 후 저장은 즉시 수행한다.
- MissionStorage mutation은 MissionManager 내부 경로만 허용한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
