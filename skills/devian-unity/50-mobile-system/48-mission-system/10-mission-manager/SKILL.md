# 10-mission-manager

MissionManager는 Mission 시스템의 오케스트레이터다.

- `MissionMessageTrigger`, `MissionScheduler`를 소유한다.
- runtime 구독은 `GameMessageTrigger`를 helper 경유로 연결한다.
- row 조건은 `conditionMsgId -> MESSAGE_META(messageType, saveType)`로 해석한다.
- trigger 입력은 `MetaMessageManager.Notify(...)`를 통해 전달된다.

---

## Responsibilities

- storage 초기화 및 복구
- daily/period anchor 보정
- scheduler rebuild/prune 호출
- `DAILY`/`PERIOD` claim 처리 및 보상 적용 위임
- 저장 호출

---

## Public API (요약)

- `InitializeAsync(...)`
- `RefreshRuntimes()`
- `GetMissionRuntimeState(missionType, missionId)`
- `GetRemainTime(missionType)`
- `ClaimAsync(missionType, missionId, ...)`
- `Notify(MESSAGE_MISSION_TYPE, ...)`
- `Subcribe(EntityId, MESSAGE_MISSION_TYPE, Handler)`
- `SubcribeOnce(EntityId, MESSAGE_MISSION_TYPE, Action<object[]>)`
- `UnSubcribe(EntityId)`

---

## Trigger 처리 규칙

- Mission runtime은 `MetaMessageManager` trigger 구독으로 갱신된다.
- 서버 시각은 `RemoteConfigManager.TryGetServerNowUtcMs(...)`로 조회한다.
- stats 선갱신 + game trigger publish + achieve notify 순서는 `MetaMessageManager` 정본을 따른다.

---

## Runtime Lifecycle 규칙

- `DAILY`:
  - cycle마다 active row를 선택해 runtime 생성/복구
- `PERIOD`:
  - 초기화/리셋 시 모든 row runtime 생성
  - 기본 WAIT 상태
  - `MISSION_PERIOD.day`를 group key로 사용해 `day(1~7)` 규칙으로 ACTIVE 전환
  - 10일 주기로 cycle reset

---

## Notes

- legacy 포맷 fallback은 사용하지 않는다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [45-meta-message-system/11-game-message-trigger](../../45-meta-message-system/11-game-message-trigger/SKILL.md)
- [16-mission-message-trigger](../16-mission-message-trigger/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
