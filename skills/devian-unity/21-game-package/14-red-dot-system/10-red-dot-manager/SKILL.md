# 10-red-dot-manager

Status: ACTIVE
AppliesTo: v10

GamePackage `RedDotManager` 설계 문서다.

---

## Implementation Location (target 3-path mirror)

- UPM (정본):
  `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/RedDot/RedDotManager.cs`
- Packages (sync):
  `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/GamePackage/Runtime/RedDot/RedDotManager.cs`
- Assets/Samples (import):
  `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{foundationVersion}/GamePackage/Runtime/RedDot/RedDotManager.cs`

---

## Target Class Design

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

---

## Responsibilities

- `CompoSingleton<RedDotManager>`로 상태 정본 1개를 유지
- key별 `RedDotNode` 저장
- parent chain 자동 생성
- `SelfOn` 변경 처리
- `ActiveChildCount` propagation
- 집계 상태 조회
- `RedDotChanged` 이벤트 발행

비책임:
- UI component 갱신
- 애니메이션
- 조건 계산
- 영속 저장
- 외부 시스템 life-cycle 관리

---

## Internal Data Model

`RedDotNode`
- `Key`
- `ParentKey`
- `SelfOn`
- `ActiveChildCount`
- `IsOn`
- `HasActiveChild`

설계 포인트:
- parent는 child 전체를 순회하지 않고 `ActiveChildCount`만으로 집계를 판단한다.
- child on/off 전환 시 parent chain은 `+1 / -1` delta만 전파한다.
- `Set("group.item001", true)` 이후 `group`은 `HasActiveChild == true`로 조회 가능하다.

---

## Event Delivery

- 내부 trigger는 `RedDotMessageTrigger`가 소유한다.
- trigger msg key는 `RED_DOT_MESSAGE_TYPE.STATE_CHANGED` 1개다.
- `Subcribe(ownerKey, key, handler)`는 trigger를 직접 노출하지 않고 exact key filter를 걸어 전달한다.
- ownerKey는 `UnityEngine.EntityId`를 사용한다.
- `ClearAll()`은 구독자를 유지한 채, 실제로 on 상태였던 key들에 false payload를 발행한다.

## Runtime Wiring

- GamePackage 런타임은 `MobileApplication` 의존을 두지 않는다.
- 샘플 앱 프리팹은 `RedDotManager`를 실제 컴포넌트로 포함해야 한다.
- 런타임 사용 진입점은 `RedDotManager.Instance`다.

---

## Notes

- `Set(key, false)`는 unknown key에 대해 no-op이다.
- `IsOn`만으로 원인을 알 수 없으므로 UI/로직에서 원인 분기가 필요하면 `TryGetState`를 사용한다.
