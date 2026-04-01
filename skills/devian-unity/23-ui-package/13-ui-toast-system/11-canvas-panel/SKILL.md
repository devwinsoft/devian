# 11-canvas-panel — UIToastCanvas + UIToastPanel

Status: ACTIVE
AppliesTo: v11

---

## 목적

Toast 전용 canvas/panel 계층.
`UIToastCanvas`는 canvas 수명주기를 담당하고,
`UIToastPanel`은 group 초기화와 request enqueue를 담당한다.

---

## SSOT

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Toast/` | 현재 workspace 구현 기준 |

### Class Signatures

#### UIToastCanvas

```csharp
namespace Devian
{
    public sealed class UIToastCanvas : UIBaseCanvas<UIToastCanvas>, IPoolable
    {
        [SerializeField] private UIToastPanel _panel;

        public UIToastPanel panel { get; }          // SerializeField → GetComponentInChildren fallback

        public override bool Validate(out string reason);  // WorldSpace 금지, panel null 금지
        public void OnPoolSpawned();
        public void OnPoolDespawned();
    }
}
```

#### UIToastPanel

```csharp
namespace Devian
{
    public sealed class UIToastPanel : UIBasePanel<UIToastCanvas>
    {
        public void Enqueue(string message, string groupId, ToastType toastType);
    }
}
```

---

## UIToastCanvas 동작

- `MobileApplication.onLoadCompletedAsync()`: `ui` label의 `UITransitionPresetAsset`을 preload한 뒤 toast canvas bootstrap을 진행한다.
- `onAwake()`: root `RectTransform`을 full-stretch + `localScale = 1`로 정규화.
- `onAwake()`: `_panel`이 null이면 `GetComponentInChildren<UIToastPanel>(true)` fallback.
- `OnPoolSpawned()`: public bridge이며 base `_HandlePoolSpawned()`를 호출한다.
- `onPoolSpawned()`: root `RectTransform` 정규화 + `_panel` 동일 fallback 재실행.
- respawn 시 base canvas/panel helper가 canvas-owned component와 panel-owned subtree를 다시 init/complete 한다.
- `OnPoolDespawned()`: public bridge이며 base `_HandlePoolDespawned()`를 호출한다.
- despawn 시 base helper가 panel-owned subtree와 canvas-owned component를 reset한다. canvas/panel 자체는 init once 상태를 유지한다.
- `Validate()`:
  - `RenderMode.WorldSpace` 금지.
  - `_panel == null` 금지.

## UIToastPanel 동작

- `onInitComplete()`: `EnsureGroups()` 호출로 group dictionary 구성.
- `Enqueue(request)`: `EnsureGroups()` 후 `ResolveGroup(request.GroupId).Enqueue(request)`.
- `onDestroy()`: 모든 group `Clear()`.
- group 설정 source는 local serialize field가 아니라 `UIToastService.Settings`다.
- group root parent는 항상 `UIToastPanel.rectTransform`이다.

### Frame 선택 규칙

- `UIToastPanel`은 frame prefab id를 local field로 들지 않는다.
- 각 `ToastGroupConfig`가 자기 `ToastFrameId`를 가진다.
- `UIToastPanel`은 group 생성 시 `config.ToastFrameId`를 그대로 `UIToastGroup`에 전달한다.
- invalid id에 대한 전역 fallback은 없다.

### EnsureGroups 규칙

- `_groups.Count > 0`이면 skip (idempotent).
- `UIToastService.GetGroupConfigs()`가 null/empty이면 default config 1개로 구성.
- duplicate `GroupId`는 LogWarning 후 skip.
- 최종 `_groups.Count == 0`이면 default config로 보강.

### ResolveGroup 규칙

- `groupId` 정규화: null/empty → `UIToastDefaults.DefaultGroupId`.
- `"Default"` → `UIToastDefaults.DefaultGroupId`로 normalize.
- 해당 key 없으면 default group fallback.
- default도 없으면 `rectTransform` 아래에 즉시 생성 후 반환.

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Service: `../10-service/SKILL.md`
- Group: `../12-group/SKILL.md`
- Canvas System: `../../10-base-system/11-ui-canvas-system/SKILL.md`
