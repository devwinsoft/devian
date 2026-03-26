# 13-ui-settings — UISettings

Status: ACTIVE
AppliesTo: v1

## Purpose

Toast/Popup/Loading 공용 전역 settings asset.
canvas ID, dim 기본값, toast group 목록, popup frame 매핑, loading canvas bootstrap 정보를 단일 `ScriptableObject`에 보관한다.

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UISettings.cs
```

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UISettings.cs` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UISettings.cs` | Packages (sync) |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/Runtime/Base/UISettings.cs` | Assets/Samples (import) |

## Asset Path

```text
Assets/Resources/Devian/UISettings.asset
```

## Shape

```csharp
[CreateAssetMenu(fileName = "UISettings", menuName = "Devian/UI/UI Settings")]
public sealed class UISettings : ScriptableObject
{
    public const string ResourcesPath = "Devian/UISettings";
    public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/UISettings.asset";

    // ── UI ──
    public string UIAddressablesKey { get; }              // default: "ui"
    public UIAssetSearchEntry[] AssetSearchEntries { get; }
    public string GetSearchDir(string key);               // key → SearchDir lookup
    public string GetResourcesSearchDir(string key);      // key → Resources relative path

    // ── Toast ──
    public UI_TOAST_CANVAS_ID ToastCanvasId { get; }
    public ToastGroupConfig[] GroupConfigs { get; }

    // ── Popup ──
    public UI_POPUP_CANVAS_ID PopupCanvasId { get; }
    public UIPopupFrameMapEntry[] PopupFrameMappings { get; }

    // ── Loading ──
    public UI_LOADING_CANVAS_ID LoadingCanvasId { get; }
}
```

## Rules

- UI 전체 settings의 단일 전역 source다.
- `UIAddressablesKey`는 `UIManager.LoadBundlesAsync()`에서 Addressables label로 사용한다.
- `AssetSearchEntries`는 UI_*_ID Selector들의 SearchDir source다. `GetSearchDir(key)`로 조회한다.
- `GetResourcesSearchDir(key)`는 `AssetSearchEntries`의 `Assets/.../Resources/...` 경로를 runtime `Resources.Load()` 경로로 변환한다.
- UI_*_ID Selector는 `BaseEditorUIAssetIdSelector` / `BaseEditorUIScriptableAssetIdSelector`를 통해 이 entries를 참조한다.
- `UIToastService`와 `UIPopupManager`가 `Resources.Load<UISettings>(UISettings.ResourcesPath)`로 load/cache한다.
- `MobileApplication.onBootAsync()`는 `Resources.Load<UISettings>(UISettings.ResourcesPath)`로 settings를 읽고 `LoadingCanvasId`를 사용해 `UILoadingCanvas`를 bootstrap한다.
- `ToastCanvasId`는 toast canvas bootstrap에 사용한다.
- `PopupCanvasId`는 popup canvas bootstrap에 사용한다.
- `LoadingCanvasId`는 loading canvas bootstrap에 사용한다.
- `LoadingCanvasId.Value`는 prefab id다.
- loading runtime path는 `GetResourcesSearchDir("UI_LOADING_CANVAS_ID") + "/" + LoadingCanvasId.Value`로 만든다.
- 현재 `UI_LOADING_CANVAS_ID` search dir는 `Assets/Resources/UI/Prefabs`이다.
- `GroupConfigs`는 toast group 설정의 source다.
- `PopupFrameMappings`는 popup frame type과 prefab id 매핑 source다.
- popup mapping editor는 popup skill의 `15-ui-popup-frame-editor`가 담당한다.

## Reference

- Parent: `../../SKILL.md`
- Toast System: `../../13-ui-toast-system/00-overview/SKILL.md`
- Popup System: `../../12-ui-popup-system/00-overview/SKILL.md`
- Loading System: `../../11-ui-loading-system/00-overview/SKILL.md`
