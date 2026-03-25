# 10-loading-canvas — UILoadingCanvas

Status: ACTIVE
AppliesTo: v11

## Purpose

`UILoadingCanvas`는 loading system의 단일 public entrypoint다.
`Spinner`, `BundleLoading`, `SceneLoading` 3개 panel의 표시 상태와 우선순위를 관리한다.
bootstrap은 `MobileApplication.onBootAsync()`에서 `UISettings.LoadingCanvasId`와
`UISettings.GetResourcesSearchDir("UI_LOADING_CANVAS_ID")`를 읽어 수행한다.

## Code Path

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Loading/UILoadingCanvas.cs
```

Bootstrap:

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/Application/MobileApplication.cs
```

Settings:

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Base/UISettings.cs
```

Prefab:

```text
framework-cs/apps/UnityExample/Assets/Resources/UI/Prefabs/uiloading_canvas.prefab
```

## Public API

```csharp
public sealed class UILoadingCanvas : UIBaseCanvas<UILoadingCanvas>
{
    public void ShowSpinner();
    public void HideSpinner();

    public void ShowBundleLoading();
    public void HideBundleLoading();
    public void SetBundleLoadingProgress(float progress);

    public void ShowSceneLoading();
    public void HideSceneLoading();
}
```

## Runtime Rules

- loading public API는 `UILoadingCanvas.Instance`만 사용한다.
- `ShowSpinner()/HideSpinner()`, `ShowBundleLoading()/HideBundleLoading()`, `ShowSceneLoading()/HideSceneLoading()`는 내부 count를 증가/감소시킨다.
- 내부 count는 `spinnerCount`, `bundleCount`, `sceneCount` 3개다.
- count는 0 미만으로 내려가지 않게 막는다.
- 실제 표시 panel은 항상 하나만 유지한다.
- panel 우선순위는 `SceneLoading > BundleLoading > Spinner`로 고정한다.
- `uiloading_canvas.prefab`이 panel view의 단일 source다.
- panel view는 코드에서 생성하지 않고 prefab serialized reference를 사용한다.
- `UISpinnerLoadingPanel`은 `dim`, `spinner` 참조를 prefab에서 받는다.
- `UIBundleLoadingPanel`은 panel background, `_progressText`, `_progressFill`을 prefab에서 받는다.
- `UISceneLoadingPanel`은 panel background를 prefab에서 받는다.
- `ShowBundleLoading()` 시 progress는 0으로 초기화한다.
- `HideBundleLoading()`으로 완전히 종료되면 progress를 초기화한다.
- 외부 progress 세팅은 `SetBundleLoadingProgress(float)`만 허용한다.

## Bootstrap Flow

```text
MobileApplication.onBootAsync()
  → Resources.Load<UISettings>(UISettings.ResourcesPath)
  → settings.LoadingCanvasId 확인
  → settings.GetResourcesSearchDir("UI_LOADING_CANVAS_ID")
  → resourcePath = searchDir + "/" + settings.LoadingCanvasId.Value
  → Resources.Load<GameObject>(resourcePath)
  → Instantiate
  → DontDestroyOnLoad
  → UILoadingCanvas.Init()
```

## References

- Overview: `../00-overview/SKILL.md`
- `UISettings`: `../../10-base-system/13-ui-settings/SKILL.md`
- `UIBaseCanvas`: `../../10-base-system/11-ui-canvas-system/SKILL.md`
