# 03-ssot — 14-red-dot-system

Status: ACTIVE
AppliesTo: v10

## SSOT Scope

이 문서는 GamePackage red dot 시스템의 정본이다.

- red dot key 표현
- 내부 노드 상태 모델
- 부모 집계 규칙
- public API
- 변경 이벤트 payload

---

## A) RedDotKey

`RedDotKey`는 red dot 상태를 식별하는 문자열 key다.

예:
- `group`
- `group.item001`
- `mail.reward`
- `mission.daily.claim`

규칙:
- `.` 기준 계층 문자열을 사용한다.
- 빈 key, 선행/후행 `.`, 빈 segment(`..`)는 invalid다.
- 등록되지 않은 key는 기본적으로 off로 본다.

---

## B) Internal Node Model

각 key는 내부적으로 `RedDotNode` 1개로 관리된다.

정본 필드:

- `Key`
- `ParentKey`
- `SelfOn`
- `ActiveChildCount`
- `IsOn = SelfOn || ActiveChildCount > 0`
- `HasActiveChild = ActiveChildCount > 0`

설명:
- `SelfOn`은 해당 key 자신에게 직접 세팅된 상태다.
- `ActiveChildCount`는 현재 on 상태인 직계 child 수다.
- `IsOn`은 최종 집계 결과다.

이 구조가 있기 때문에
`Set("group.item001", true)` 후 `group.HasActiveChild == true`를 판별할 수 있다.

---

## C) Key Creation Policy

- `Set(key, true)`는 leaf와 필요한 parent key를 동적으로 생성한다.
- `Set(key, false)` 또는 `Clear(key)`는 기존 key가 없으면 no-op이다.
- parent key는 child 집계를 위해 자동 생성될 수 있다.

---

## D) Propagation Rules

예:

```text
group
└ group.item001
```

흐름:

1. `Set("group.item001", true)`
2. `group.item001.SelfOn = true`
3. `group.item001.IsOn = true`
4. parent `group.ActiveChildCount += 1`
5. `group.IsOn = true`
6. `group.HasActiveChild = true`

반대 흐름:

1. `Set("group.item001", false)`
2. child `IsOn`이 false로 내려가면
3. parent `group.ActiveChildCount -= 1`
4. 남은 active child가 없고 `group.SelfOn == false`면 `group.IsOn = false`

---

## E) Public API

```csharp
public sealed class RedDotManager : CompoSingleton<RedDotManager>
{
    public void Set(string key, bool value);
    public void Clear(string key);
    public void ClearAll();

    public bool IsOn(string key);
    public bool Contains(string key);
    public bool HasActiveChild(string key);
    public bool TryGetState(string key, out RedDotStateView state);

    public void Subcribe(EntityId ownerKey, string key, Action<RedDotChanged> handler);
    public void UnSubcribe(EntityId ownerKey);
}
```

정책:
- 상태 정본 접근은 `RedDotManager.Instance`를 사용한다.
- 구독 ownerKey는 `UnityEngine.EntityId`를 사용한다.
- `IsOn`만으로는 self on인지 child 집계 on인지 구분하지 않는다.
- 원인 구분은 `HasActiveChild` 또는 `TryGetState`를 사용한다.
- `Clear(key)`는 v1에서 `Set(key, false)`와 동일하다.

---

## F) State Snapshot

public 조회 정본:

```csharp
public readonly struct RedDotStateView
{
    public string Key { get; }
    public bool IsOn { get; }
    public bool SelfOn { get; }
    public bool HasActiveChild { get; }
}
```

---

## G) Event Model

trigger key:

```csharp
public enum RED_DOT_MESSAGE_TYPE
{
    NONE = 0,
    STATE_CHANGED = 1,
}
```

payload 정본:

```csharp
public sealed class RedDotChanged
{
    public string Key { get; }
    public bool IsOn { get; }
    public bool SelfOn { get; }
    public bool HasActiveChild { get; }
}
```

알림 규칙:
- `RedDotManager`는 내부 상태 스냅샷이 실제로 변한 key만 이벤트를 발행한다.
- payload에 `Key`, `IsOn`, `SelfOn`, `HasActiveChild`를 포함한다.
- `BaseTrigger` msg key는 `STATE_CHANGED` 1개만 사용하고, 실제 red dot key 필터는 `RedDotManager.Subcribe(...)` wrapper가 수행한다.

---

## H) Non-goals (v1)

v1에서 지원하지 않는다.

- 숫자 badge
- 서버 연동
- 저장/복원
- batch update 최적화
- 조건식 자동 평가
- UI binding
