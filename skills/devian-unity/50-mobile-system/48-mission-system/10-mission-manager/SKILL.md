# 10-mission-manager

MissionManager는 Mission 시스템의 오케스트레이터다.

- `MissionMessageTrigger`, `MissionScheduler`를 소유한다.
- daily runtime 구독은 `GameMessageTrigger`를 helper 경유로 연결한다.
- row 조건은 `messageId -> MESSAGE(messageType, saveType)`로 해석한다.
- trigger 입력은 `GameMessageManager.Notify(...)`를 통해 전달된다.

---

## Responsibilities

- storage 초기화 및 복구
- daily anchor 보정
- scheduler rebuild/prune 호출
- claim 처리 및 보상 적용 위임
- level-up 전환 orchestration
- save 호출

---

## Public API (요약)

- `InitializeAsync(...)`
- `RefreshRuntimes()`
- `GetMissionRuntimeState(missionType, missionId)`
- `GetRemainTime(missionType)`
- `ClaimAsync(missionType, missionId, ...)`
- `Notify(MISSION_MESSAGE, ...)`
- `Subcribe(EntityId, MISSION_MESSAGE, Handler)`
- `SubcribeOnce(EntityId, MISSION_MESSAGE, Action<object[]>)`
- `UnSubcribe(EntityId)`

---

## Trigger 처리 규칙

- `MissionManager` daily runtime은 `GameMessageManager` trigger 구독으로 갱신된다.
- 서버 시각은 `RemoteConfigManager.TryGetServerNowUtcMs(...)`로 조회한다.
- stats 선갱신 + game trigger publish + achieve notify 순서는 `GameMessageManager` 정본을 따른다.

---

## Claim / Level-Up 규칙

- claim 성공 시:
  - reward apply
  - runtime 상태 갱신
  - message notify
  - save 실행

---

## Notes

- legacy 포맷 fallback은 사용하지 않는다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [45-game-message-system/11-game-message-trigger](../../45-game-message-system/11-game-message-trigger/SKILL.md)
- [16-mission-message-trigger](../16-mission-message-trigger/SKILL.md)
- [12-mission-storage](../12-mission-storage/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
