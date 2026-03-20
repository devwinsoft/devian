# 52-ui-toast-system — Overview

Status: ACTIVE
AppliesTo: v11

Overlay 기반 non-blocking toast system.
toast canvas bootstrap은 `MobileApplication`이 담당하고,
전역 settings source는 `UIToastService`가 담당하며,
런타임 표시/queue/duplicate 처리는 `UIToastService` → `UIToastPanel` → `UIToastGroup` → `UIToastFrame` 계층이 담당한다.
toast frame prefab 참조와 group 설정은 `UIToastSettings.asset`에서 공급한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | Canvas Ownership / Text Boundary / Non-Blocking / Tween Boundary / Pool Cleanup |
| [10-service](../10-service/SKILL.md) | UIToastService — 외부 진입점, Show API |
| [11-canvas-panel](../11-canvas-panel/SKILL.md) | UIToastCanvas + UIToastPanel — canvas 계층 및 group 초기화 |
| [12-group](../12-group/SKILL.md) | UIToastGroup — queue / duplicate / max-visible / layout |
| [13-frame](../13-frame/SKILL.md) | UIToastFrame — pool / tween / lifetime |
| [14-data-model](../14-data-model/SKILL.md) | ToastRequest / ToastGroupConfig / Enums / UIToastDefaults |
| [15-ui-toast-frame-id](../15-ui-toast-frame-id/SKILL.md) | UI_TOAST_FRAME_ID — UIToastFrame 프리팹 참조 ID (AssetId 패턴) |
| [16-ui-toast-settings](../16-ui-toast-settings/SKILL.md) | UIToastSettings — Resources 기반 전역 toast group 설정 asset |

---

## Scope

### Includes
- `UIToastService`
- `UIToastCanvas`
- `UIToastPanel`
- `UIToastGroup`
- `UIToastFrame`
- group별 queue / duplicate / max-visible 정책
- `MobileApplication` 기반 toast canvas bootstrap
- `UIToastSettings.asset` 기반 전역 group 설정

### Excludes
- popup / blocker / dim
- `TEXT_ID` 직접 처리
- interaction / click
- business logic

---

## Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/
├── UIToastService.cs
├── UIToastCanvas.cs
├── UIToastPanel.cs
├── UIToastGroup.cs
├── UIToastFrame.cs
├── ToastRequest.cs
├── ToastGroupConfig.cs
├── UIToastSettings.cs
├── ToastEnums.cs
├── UIToastDefaults.cs
└── UI_TOAST_FRAME_ID.cs
```

Bootstrap:
```
framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Application/MobileApplication.cs
```

---

## Runtime Flow

### Bootstrap

```
MobileApplication.onLoadCompletedAsync()
  → find existing UIToastCanvas
  → if missing and UI_CANVAS_ID valid:
        BundlePool.Spawn<UIToastCanvas>(id)
  → DontDestroyOnLoad(canvas)
  → canvas.Init()
```

### Show

```
Caller
  → UIToastService.Show(...)
  → ResolvePanel() : UIToastCanvas.Instance 또는 FindAnyObjectByType
  → UIToastPanel.Enqueue(request)
  → UIToastGroup.Enqueue(request)
      → duplicate 처리 (DuplicatePolicy)
      → max-visible 미만이면 ShowImmediate
      → max-visible 초과이면 _pending queue에 적재
  → UIToastFrame.Show(duration, onHidden)
      → UIToastFrame 소유 show transition id 재생
      → CoLifetime 코루틴 시작
  → lifetime 만료
      → UIToastFrame.HideInternal()
      → UIToastFrame 소유 hide transition id 재생
  → hide tween 완료
      → UIToastGroup.OnFrameHidden()
      → BundlePool.Despawn(frame)
      → FlushQueue() → 다음 pending show
```

---

## Related

- Parent: `../SKILL.md`
- Canvas System: `../11-ui-canvas-system/SKILL.md`
- UITweenSystem: `../51-ui-tween-system/00-overview/SKILL.md`
