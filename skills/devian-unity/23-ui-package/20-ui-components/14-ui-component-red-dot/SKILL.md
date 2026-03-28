# 14-ui-component-red-dot

Status: ACTIVE
AppliesTo: v11

## Overview

`RedDotManager` 상태를 `UnityEngine.UI.Image.enabled`에 바인딩하는 UI 플러그인.
`UIComponentBase` 초기화 시 현재 red dot 상태를 즉시 반영하고, 이후 key 변경 이벤트를 구독한다.

---

## SSOT

### Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentRedDot.cs
```

### Serialized Fields

| Field | Type | Description |
|-------|------|-------------|
| `_redDotKey` | `string` | `RedDotManager` 조회/구독 key |

### Required Component

- `Image` (`RequireComponent(typeof(Image))`)

---

## Lifecycle

| Event | Action |
|-------|--------|
| `onAwake()` | self `Image` 캐시 |
| `onInit(Canvas)` | key가 비어 있으면 `enabled=false` 후 종료. 아니면 `RedDotManager.Instance.IsOn(key)`로 초기 동기화 후 `Subcribe(GetEntityId(), key, ...)` |
| `onDestroy()` | `RedDotManager.Instance.UnSubcribe(GetEntityId())` |

### Handler

- `onRedDotChanged(RedDotChanged changed)`:
  `_image.enabled = changed.IsOn`

---

## Policy

- 이 컴포넌트는 `Image.enabled`만 제어한다.
- `gameObject.SetActive`, alpha/color, 애니메이션, badge count는 담당하지 않는다.
- host scene/prefab은 `RedDotManager` singleton을 포함해야 한다.
- 구독 replay가 없으므로 `onInit(Canvas)`에서 `IsOn()` 선조회 후 subscribe 순서를 유지한다.
- ownerKey는 `UnityEngine.EntityId` (`GetEntityId()`)를 사용한다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIComponentBase` | `com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentBase.cs` |
| `Image` | `UnityEngine.UI` |
| `RedDotManager` | `com.devian.foundation/Samples~/GamePackage/Runtime/RedDot/RedDotManager.cs` |
| `RedDotChanged` | `com.devian.foundation/Samples~/GamePackage/Runtime/RedDot/RedDotChanged.cs` |

---

## Related

- Parent: [00-overview](../00-overview/SKILL.md)
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- Red dot: [21-game-package/14-red-dot-system/10-red-dot-manager](../../../21-game-package/14-red-dot-system/10-red-dot-manager/SKILL.md)
