# 03-ssot — 45-game-message-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 아래 항목의 정본이다.

- `GameMessageManager` 클래스의 1차 책임 범위
- Game message trigger 라우터 타입 규약
- Message stat 저장 모델(`GameMessageStorage`) 정본

---

## A) Types

- `GameMessageManager`
- `GameMessageStorage`
- `GameMessageTrigger : BaseTrigger<int, GAME_MESSAGE_TYPE>`

공통 trigger 동작 정본:
- [20-domain-common-system/25-trigger](../../../20-domain-common-system/25-trigger/SKILL.md)

---

## B) Game Message Trigger Contract

- ownerKey: subscriber-defined `int`
- key: `GAME_MESSAGE_TYPE`
- 외부 진입점: `MissionManager.Notify(...)` (GameMessageManager helper 경유)

처리 순서:
1. `MissionManager.Notify(...)` 또는 `GameMessageManager.NotifyGameMessage(...)` 진입
2. `GameMessageManager`가 `TB_MESSAGE` 기준 `message.stats[messageId]` 갱신
3. `GameMessageManager` game trigger publish
4. mission runtime 구독자 수신 (GameMessageTrigger direct)
5. `GameMessageManager`가 `AchieveManager.Notify(messageType, delta)` 호출

---

## C) Message Storage Contract

- 저장 위치: root payload `message`
- schema:
  - `schemaVersion: int`
  - `stats: Dictionary<string, CBigInt>` (key = `messageId`)
- migration:
  - 구버전(`v12`)의 `mission.stats`는 load 시 `message.stats`로 이동한다.
  - write는 `message.stats`만 사용한다.

---

## Related

- [10-game-message-manager](../10-game-message-manager/SKILL.md)
- [11-game-message-trigger](../11-game-message-trigger/SKILL.md)
- [14-game-message-storage](../14-game-message-storage/SKILL.md)
- [48-mission-system/16-mission-message-trigger](../../48-mission-system/16-mission-message-trigger/SKILL.md)
- [48-mission-system/03-ssot](../../48-mission-system/03-ssot/SKILL.md)
