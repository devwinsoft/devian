# 11-ui-loading-system — Overview

Status: ACTIVE
AppliesTo: v11

`UIPackage`의 전역 loading canvas 시스템이다.
실제 구현은 `UILoadingCanvas`, `UISpinnerLoadingPanel`, `UIBundleLoadingPanel`, `UISceneLoadingPanel`,
`UI_LOADING_CANVAS_ID`, `UISettings.LoadingCanvasId`, `MobileApplication.onBootAsync()`로 구성된다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-loading-canvas](../10-loading-canvas/SKILL.md) | `UILoadingCanvas` API / panel 우선순위 / bootstrap |
| [13-ui-settings](../../10-base-system/13-ui-settings/SKILL.md) | `LoadingCanvasId`, `AssetSearchEntries`, `GetResourcesSearchDir()` |
| [11-ui-canvas-system](../../10-base-system/11-ui-canvas-system/SKILL.md) | `UIBaseCanvas` / `UIBasePanel` 수명주기 |
| [11-mobile-application](../../../50-mobile-package/11-mobile-application/SKILL.md) | `MobileApplication.onBootAsync()` bootstrap 진입점 |

---

## Runtime Shape

- public entrypoint는 `UILoadingCanvas.Instance`다.
- panel 우선순위는 `SceneLoading > BundleLoading > Spinner`다.
- `SetBundleLoadingProgress(float)`만 외부 progress 입력을 받는다.
- loading view는 `Assets/Resources/UI/Prefabs/uiloading_canvas.prefab`에 고정한다.
- panel view는 코드에서 생성하지 않고 prefab serialized reference를 사용한다.
- loading canvas bootstrap은 `MobileApplication.onBootAsync()`에서 수행한다.
- runtime path는 `UISettings.GetResourcesSearchDir("UI_LOADING_CANVAS_ID") + "/" + UISettings.LoadingCanvasId.Value`로 만든다.

---

## Code Path

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Loading/
```

Loading prefab:

```text
framework-cs/apps/UnityExample/Assets/Resources/UI/Prefabs/uiloading_canvas.prefab
```

Bootstrap:

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/MobilePackage/Runtime/Application/MobileApplication.cs
```
