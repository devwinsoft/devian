# 10-mission-manager

MissionManager는 Mission 시스템의 오케스트레이터다.

- `MissionTriggerSystem`, `MissionMessageSystem`, `MissionScheduler`를 소유한다.
- row 조건은 `missionStatId -> MISSION_STAT(statType, opType)`로 해석한다.
- trigger 입력 시 `stats` 선갱신 후 runtime notify 순서를 보장한다.

---

## Responsibilities

- storage/clock 초기화 및 복구
- daily anchor 보정
- scheduler rebuild/prune 호출
- claim 처리 및 보상 적용 위임
- level-up 전환 orchestration
- save 호출

---

## Public API (요약)

- `InitializeAsync(...)`
- `RefreshClockAsync(...)`
- `RefreshRuntimes()`
- `GetMissionRuntimeState(missionType, missionId)`
- `GetRemainTime(missionType)`
- `ClaimAsync(missionType, missionId, ...)`
- `Notify(MISSION_STAT_TYPE, long/int/CBigInt)`
- `Subcribe(EntityId, MISSION_MESSAGE, Handler)`
- `SubcribeOnce(EntityId, MISSION_MESSAGE, Action<object[]>)`
- `UnSubcribe(EntityId)`

---

## Trigger 처리 규칙

- `MissionManager.Notify(...)` 내부에서 `TB_MISSION_STAT`를 순회해 `stats[missionStatId]`를 먼저 갱신:
  - `SUM`: `current + delta`
  - `MAX`: `max(current, delta)`
- 이후 내부 `MissionTriggerSystem`으로 runtime notify를 호출한다.

---

## Claim / Level-Up 규칙

- claim 성공 시:
  - reward apply
  - runtime 상태 갱신
  - message notify
  - save 실행
- `ACHIEVE`에서 다음 row가 존재하면 같은 runtime level-up:
  1. 기존 statType 구독 해지
  2. level/state 갱신
  3. 새 `missionStatId/statType/opType/conditionValue` 바인딩
  4. 새 stat reader(`stats[missionStatId]`) 연결
  5. 새 statType 재구독

---

## Notes

- `LevelUp`에서 progress 수동 set은 하지 않는다.
- `ACHIEVE` progress 정본은 항상 `MissionStorage.stats[missionStatId]`.
- legacy 포맷 fallback은 사용하지 않는다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-mission-trigger-system](../11-mission-trigger-system/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
