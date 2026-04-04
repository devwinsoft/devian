# 13-frame — UIToastFrame

Status: ACTIVE
AppliesTo: v11

---

## 목적

개별 toast 한 장을 담당하는 pooled frame.
message/color bind, show/hide tween, lifetime coroutine, non-blocking 강제를 수행한다.

---

## SSOT

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UIToastFrame.cs` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UIToastFrame.cs` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Toast/UIToastFrame.cs` | 현재 workspace 구현 기준 |

### Class Signature

```csharp
namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    public sealed class UIToastFrame : UIBaseFrame, IPoolable
    {
        // ─── Inspector ───
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private UI_TRANSITION_PRESET_ID _showTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _hideTransitionId;
        [SerializeField] private Graphic _backgroundGraphic;
        [SerializeField] private CanvasGroup _canvasGroup;

        // Background/Text colors per ToastType (Info/Success/Warning/Error)

        // ─── State ───
        public bool isHiding { get; }
        private string _currentMessage;
        private LayoutSnapshot _initialLayout;

        // ─── Pool ───
        public void OnPoolSpawned();    // base _HandlePoolSpawned bridge
        public void OnPoolDespawned();  // base _HandlePoolDespawned bridge

        // ─── Control ───
        public void Bind(string message, ToastType toastType);
        public bool HasMessage(string message);                     // Ordinal 비교
        public void Show(float duration, Action<UIToastFrame> onHidden);
        public void RefreshDuration(float duration);                // isHiding이면 무시
        internal void ApplyGroupOffset(Vector2 offset);            // prefab 기본 RectTransform 복원 후 offset 적용
    }
}
```

---

## Inspector 기본값 (ResolveDefaults)

모든 `[SerializeField]`가 null이면 same-GO / child fallback:

| 필드 | fallback |
|------|---------|
| `_messageText` | `GetComponentInChildren<TextMeshProUGUI>(true)` |
| `_backgroundGraphic` | `GetComponent<Image>()` → `GetComponentInChildren<Image>(true)` |
| `_canvasGroup` | `GetComponentInChildren<CanvasGroup>(true)` |

`UITransitionPlayer`는 `[RequireComponent(typeof(UITransitionPlayer))]`로 같은 GameObject에 필수다.

---

## Non-Blocking (ApplyNonBlocking)

- `_canvasGroup.interactable = false`
- `_canvasGroup.blocksRaycasts = false`
- `GetComponentsInChildren<Graphic>(true)` 전체에 `raycastTarget = false`

spawn 시(`OnPoolSpawned`) 및 bind 시(`Bind`) 두 번 호출된다.

## Pool Contract

- public `OnPoolSpawned()` / `OnPoolDespawned()`는 각각 base `_HandlePoolSpawned()` / `_HandlePoolDespawned()` bridge다.
- `onPoolSpawned()`는 `ResolveDefaults()` + `CancelInternal()` + `ApplyNonBlocking()`를 수행한다.
- `onPoolDespawned()`는 `CancelInternal()` 후 `_messageText.text`와 `_currentMessage`를 비운다.
- base `UIBaseFrame`가 despawn 시 init state를 reset하므로, 다음 respawn 뒤 group이 `_Init()` / `_InitComplete()`를 다시 호출하면 `onInit()`이 재실행된다.

## Bind 규약

- `_currentMessage = request.Message`
- `_messageText.text = _currentMessage`
- `_messageText.ForceMeshUpdate()` 호출
- `_messageText == null`이면 warning 로그 출력
- `HasMessage()`는 TMP text가 아니라 `_currentMessage`를 기준으로 비교한다

## Layout 규약

- `onAwake()` / `onInit()`에서 prefab 원본 `RectTransform` 값을 snapshot으로 캐시한다.
- `ApplyGroupOffset(offset)`는 anchor/pivot/sizeDelta/localScale/anchoredPosition을 원본 값으로 복원한 뒤 offset만 더한다.
- `ApplyGroupOffset(offset)` 직후 `UITransitionPlayer.RefreshBaseline()`을 호출해 show/hide move tween의 기준점을 현재 슬롯 좌표로 동기화한다.
- 따라서 group은 frame 기본 크기를 덮어쓰지 않는다.

---

## Show / Lifetime 흐름

```
Show(duration, onHidden)
  → CancelInternal() (이전 코루틴/tween 정리)
  → isHiding = false
  → same-GO UITransitionPlayer + _showTransitionId 기반 preset 재생
  → RestartLifetime(duration)
      → CoLifetime(duration) 코루틴 시작
      → WaitForSecondsRealtime(duration)
      → HideInternal()
          → isHiding = true
          → same-GO UITransitionPlayer + _hideTransitionId 기반 preset 재생
          → handle null 또는 IsCanceled → NotifyHidden() 즉시 호출
```

---

## Hide tween 완료 → Despawn

- `NotifyHidden()` → `_onHidden?.Invoke(this)` → `UIToastGroup.OnFrameHidden(frame)`
- `OnFrameHidden`에서 `BundlePool.Despawn(frame)` 호출
- slot release는 **lifetime 만료가 아니라 hide tween 완료** 기준

---

## Color 테이블 (default)

| ToastType | Background | Text |
|-----------|-----------|------|
| Info | `(0.15, 0.15, 0.15, 0.95)` | White |
| Success | `(0.16, 0.45, 0.24, 0.95)` | White |
| Warning | `(0.75, 0.50, 0.08, 0.95)` | White |
| Error | `(0.70, 0.18, 0.18, 0.95)` | White |

Inspector에서 색상 override 가능.

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Group: `../12-group/SKILL.md`
- Frame ID: `../15-ui-toast-frame-id/SKILL.md`
- UITransitionPlayer: `../../22-ui-tween-system/14-ui-transition-player/SKILL.md`
