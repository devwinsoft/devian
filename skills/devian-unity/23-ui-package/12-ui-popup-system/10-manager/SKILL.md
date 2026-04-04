# 10-manager — UIPopupManager

Status: ACTIVE
AppliesTo: v1

## Purpose

popup stack의 단일 owner.
show / close / duplicate / back / dim 상태 갱신을 담당한다.

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupManager.cs
```

## Class Shape

```csharp
public sealed class UIPopupManager : AutoSingleton<UIPopupManager>
{
    public UISettings Settings { get; }
    public void Initialize();
    public bool Show(UI_POPUP_FRAME_ID frameId, object payload = null, Action<PopupCloseReason> onClosed = null);
    public bool CloseTop(PopupCloseReason reason = PopupCloseReason.Cancel);
    public void CloseAll();
    internal void HandleDimClicked();
}
```

## Initialize 동작

`MobileApplication.onLoadCompletedAsync()` → `UIPopupManager.Instance.Initialize()` 호출.

```text
1. UIPopupCanvas.Instance 조회
2. null이면 FindAnyObjectByType<UIPopupCanvas>(FindObjectsInactive.Include)
3. 여전히 null이면:
   a. ResolveSettings() → settings.PopupCanvasId 읽기
   b. IsValid 검사 (invalid이면 return)
   c. BundlePool.Spawn<UIPopupCanvas>(canvasId)
4. DontDestroyOnLoad(canvas)
5. canvas.isInitialized가 false이면 canvas.Init()
```

## Resolve Flow

`UIPopupManager`는 다음 경로로 panel을 resolve한다.

```text
UIPopupCanvas.Instance
  -> null이면 FindAnyObjectByType<UIPopupCanvas>
  -> canvas.isInitialized && canvas.isInitComplete 확인
  -> canvas.panel 반환
```

settings는 `Resources.Load<UISettings>(UISettings.ResourcesPath)`로 load/cache한다.

## Runtime Notes

- duplicate policy는 `Show(frameId, ...)` 진입점에서 먼저 처리한다.
- popup prefab id는 caller가 `UI_POPUP_FRAME_ID`로 직접 넘긴다.
- payload는 `object`로 전달되며, frame의 `onBind(object payload)`에서 수신한다.
- frame concrete type은 `UISettings.PopupFrameMappings`에서 `frameId → FrameTypeName → Type`으로 resolve한다.
- duplicate matching 기준은 `frameId`(string) 값이다.
- `FocusIfShow`는 기존 entry를 `remove -> push`로 재배치한다.
- `ReplaceIfShow`는 기존 popup을 `Cancel` reason으로 close 시작 후 새 popup을 show한다.
- close callback은 `PopupCloseReason`만 받는다.
- top modal state는 top frame instance의 policy property에서 읽는다.
- `CloseAll()`은 frame transition을 기다리지 않고 즉시 despawn + callback 호출로 정리한다.
- back / escape 입력은 `Update()`에서 top popup 기준으로 처리한다.
