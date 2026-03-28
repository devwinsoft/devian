# 14-red-dot-system — Overview

Status: ACTIVE
AppliesTo: v10

GamePackage용 red dot 상태 시스템 개요다.
이 시스템은 계층형 key의 on/off 상태, 부모 집계, `BaseTrigger` 기반 변경 알림만 담당한다.

핵심:
- 상태 정본은 `CompoSingleton<RedDotManager>` 1개다.
- key는 `"group.item001"` 같은 `.` 구분 계층 문자열이다.
- 상태 모델은 `SelfOn + ActiveChildCount + IsOn`으로 유지한다.
- 부모 key는 자식 집계 결과를 반영한다.
- UI binding, prefab, 저장/복원, count badge는 범위 밖이다.

---

## Sample SSOT

- `com.devian.foundation/Samples~/GamePackage`

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 모듈 경계와 하드룰 |
| [03-ssot](../03-ssot/SKILL.md) | key/state/event/API 정본 |
| [10-red-dot-manager](../10-red-dot-manager/SKILL.md) | `RedDotManager` 설계와 구현 위치 |
| [11-red-dot-message-trigger](../11-red-dot-message-trigger/SKILL.md) | `BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>` 라우팅 계약 |

---

## Related

- [../00-overview](../../00-overview/SKILL.md) — GamePackage 개요
- [../../20-common-package/25-trigger](../../../20-common-package/25-trigger/SKILL.md) — `BaseTrigger` 공통 규약
- [../../23-ui-package/00-overview](../../../23-ui-package/00-overview/SKILL.md) — UI는 이 시스템 바깥에서 연결
