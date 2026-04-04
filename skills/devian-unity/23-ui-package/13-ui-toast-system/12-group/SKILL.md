# 12-group — UIToastGroup

Status: ACTIVE
AppliesTo: v11

---

## 목적

group 단위 queue, duplicate 처리, max-visible 제한, layout을 담당하는 **non-MonoBehaviour** 런타임 객체.
`UIToastPanel`이 group당 1개 인스턴스를 소유한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UIToastGroup.cs
```

### Class Signature

```csharp
namespace Devian
{
    public sealed class UIToastGroup
    {
        public UIToastGroup(
            UIToastCanvas canvas,
            RectTransform root,
            UI_TOAST_FRAME_ID toastFrameId,
            ToastGroupConfig config);

        public void Enqueue(string message, ToastType toastType);
        public void Clear();
    }
}
```

---

## Enqueue 흐름

```
1. TryHandleDuplicate(request)
   → DuplicatePolicy에 따라 처리 (아래 표 참조)
   → true 반환 시 → 여기서 종료 (request 소비됨)

2. _active.Count < ResolveMaxVisibleCount()
   → true  → ShowImmediate(request)
   → false → _pending.Enqueue(request)
```

### DuplicatePolicy 처리 표

| Policy | 동작 |
|--------|------|
| `Allow` | 항상 false 반환 (중복 무시 없음) |
| `IgnoreIfVisible` | 동일 message가 active에 있으면 true 반환 (skip) |
| `RefreshDurationIfVisible` | 동일 message가 active에 있으면 duration 갱신 후 true 반환 |

"동일 message" 기준: `StringComparison.Ordinal` (HasMessage 위임).

---

## ShowImmediate 흐름

```
1. `toastFrameId` invalid면 error log 후 종료
2. BundlePool.Spawn<UIToastFrame>(toastFrameId, parent: _root)
3. frame.isFrameInitialized 미완료 시: frame._Init + _InitComplete 직접 호출
4. frame.Bind(request)
5. frame.transform.SetAsLastSibling()
6. 현재 active frame들의 실제 높이를 누적해서 next local offset 계산
7. frame.ApplyGroupOffset(config.AnchoredOffset + nextOffset)
8. _active.Add(frame)
9. frame.Show(duration, OnFrameHidden)
```

---

## OnFrameHidden 흐름

```
1. _active.Remove(frame)
2. BundlePool.Despawn(frame)
3. Relayout()
4. FlushQueue()
```

FlushQueue: `_active.Count < MaxVisible && _pending.Count > 0`인 동안 dequeue → duplicate 재검사 → ShowImmediate.

---

## Layout 규약

- `_root`는 anchorMin `(0,0)` / anchorMax `(1,1)` / sizeDelta `0`의 full-stretch rect다.
- `_root`는 frame size를 결정하지 않는다.
- 각 frame은 prefab의 원본 `RectTransform` 값을 유지한다.
- `config.AnchorPreset`은 `_root.rect.size - frameSize`를 기준으로 계산한 anchor offset으로 반영한다.
- active frame은 생성 순서대로 `LayoutDirection.Down`이면 y 음수 방향, `LayoutDirection.Up`이면 y 양수 방향으로 적층한다.
- 각 frame은 원본 `anchoredPosition`을 복원한 뒤 `anchorOffset + config.AnchoredOffset + localOffset`을 더한다.
- frame size는 `LayoutUtility.GetPreferredWidth/Height`를 우선 사용하고, 값이 `0 이하`면 `rect.width/height`로 fallback.
- 새 frame의 local offset은 `active.Count * 고정값`이 아니라, 현재 visible frame들의 실제 높이와 spacing 누적값으로 계산한다.
- `DuplicatePolicy = Allow`일 때 새 frame은 base slot을 재사용하지 않고 stack의 끝 슬롯에 append된다.
- spacing: `UIToastDefaults.DefaultSpacing`.

---

## MaxVisibleCount

- `Mathf.Max(1, config.MaxVisibleCount)` — 최소 1 보장.

## Duration

- `request.DurationOverride`가 있으면 override 우선.
- 없으면 `config.DefaultDuration` 사용.
- `config.DefaultDuration <= 0`이면 `UIToastDefaults.DefaultDuration`으로 fallback.

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Canvas/Panel: `../11-canvas-panel/SKILL.md`
- Frame: `../13-frame/SKILL.md`
- Data Model: `../14-data-model/SKILL.md`
