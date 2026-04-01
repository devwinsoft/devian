# 45-game-message-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

MobilePackage의 메시지/트리거 계층 하드룰을 정의한다.

---

## Hard Rules

### 1) Trigger/Message 라우팅 정본은 `BaseTrigger`다

- 공통 동작 정본은 [20-common-package/25-trigger](../../../20-common-package/25-trigger/SKILL.md)다.
- MobilePackage 하위 시스템은 `BaseTrigger<TOwnerKey, TMsgKey>`를 래핑해서 사용한다.

### 2) Game message trigger는 45-game-message-system이 소유한다

- `GameMessageTrigger`은 `GameMessageManager` 내부 소유다.
- trigger 인스턴스는 외부에 직접 노출하지 않는다.
- `GameMessageManager` helper를 통해서만 publish/subscribe한다.

### 3) Mission message trigger는 48-mission-system이 소유한다

- `MissionMessageTrigger` 문서는 [48-mission-system/16-mission-message-trigger](../../48-mission-system/16-mission-message-trigger/SKILL.md)에서 관리한다.
- 외부 구독자는 `MissionManager.Subcribe(...)` 계열 API를 사용한다.

### 4) Mission notify 순서는 유지한다

- `message.stats[message_id]` 갱신
- mission runtime trigger notify
- achieve notify

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-game-message-manager](../10-game-message-manager/SKILL.md)
- [48-mission-system/01-policy](../../48-mission-system/01-policy/SKILL.md)
