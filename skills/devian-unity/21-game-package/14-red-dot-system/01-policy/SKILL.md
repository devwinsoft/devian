# 14-red-dot-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

GamePackage red dot 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) Red dot 시스템은 상태 데이터와 전파만 담당한다

- 책임은 key 상태 저장, 부모 집계, 조회, 변경 이벤트 발행이다.
- UI component, animation, prefab, binding 코드는 포함하지 않는다.
- 조건 계산은 외부 시스템이 수행하고 최종 `Set(key, bool)`만 전달한다.

### 2) 상태 정본은 `RedDotManager` 단일 인스턴스가 소유한다

- `RedDotManager`는 `CompoSingleton<RedDotManager>`다.
- GamePackage 런타임은 `MobileApplication`에 의존하지 않는다.
- host scene/prefab은 `RedDotManager`를 직접 컴포넌트로 포함한다.
- 개별 노드 mutate는 `RedDotManager` 경유로만 수행한다.
- 외부는 내부 `Dictionary<string, RedDotNode>`를 직접 만지지 않는다.
- `RedDotMessageTrigger` 인스턴스는 외부에 직접 노출하지 않는다.

### 3) 메시지 라우팅 정본은 `BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>`다

- 변경 이벤트는 `RedDotMessageTrigger : BaseTrigger<EntityId, RED_DOT_MESSAGE_TYPE>`로 발행한다.
- 외부 구독자는 `RedDotManager.Subcribe(...)`, `UnSubcribe(...)` helper만 사용한다.
- `ownerKey`는 `UnityEngine.EntityId`를 사용한다.

### 4) leaf key write를 권장한다

- 부모 key direct set은 허용하지만 자식 집계와 함께 동작한다.
- 실제 데이터 소스는 leaf key 중심으로 `Set`하는 것을 권장한다.
- 부모가 왜 켜졌는지 확인하려면 `IsOn(key)`가 아니라 `HasActiveChild(key)` 또는 `TryGetState(key, out ...)`를 사용한다.

### 5) public 조회는 집계 상태와 원인 상태를 구분한다

- `IsOn(key)`는 최종 집계 on/off만 반환한다.
- `HasActiveChild(key)`는 자식 집계 때문에 켜졌는지 여부를 반환한다.
- `TryGetState(key, out state)`는 `SelfOn`, `HasActiveChild`, `IsOn` 스냅샷을 반환한다.

### 6) `ClearAll()`은 구독을 지우지 않는다

- `ClearAll()`은 red dot 상태 데이터만 초기화한다.
- `BaseTrigger.ClearAll()`을 그대로 외부에 노출하지 않는다.
- 기존 subscriber는 유지되고, 상태가 실제로 바뀐 key에 대해서만 false 이벤트를 받는다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-red-dot-manager](../10-red-dot-manager/SKILL.md)
- [11-red-dot-message-trigger](../11-red-dot-message-trigger/SKILL.md)
