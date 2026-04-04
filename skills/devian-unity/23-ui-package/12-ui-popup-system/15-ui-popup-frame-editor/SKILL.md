# 15-ui-popup-frame-editor — Popup Frame Editor UX

Status: ACTIVE
AppliesTo: v1

## Purpose

popup frame id 선택 UX를 담당한다.

- `UI_POPUP_FRAME_ID` drawer/selector를 제공한다

## Target Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UI_POPUP_FRAME_ID_Drawer.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameIdSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UISettingsEditor.cs
```

## UX Goal

- `Frame Id`는 `Select` 버튼으로 prefab을 고른다

## Rules

- `FrameId`는 `UI_POPUP_FRAME_ID` property drawer를 재사용한다
- selector는 `UISettings.GetSearchDir("UI_POPUP_FRAME_ID")` 기준으로 popup prefab id를 보여준다
- popup frame type mapping editor는 두지 않는다. runtime이 prefab의 실제 component type으로 resolve한다.

## Current Behavior

- `UISettingsEditor`
  - 기본 `UISettings` serialized field inspector만 제공

## Dependency

- Parent: `../00-overview/SKILL.md`
- Related: `../16-ui-popup-settings/SKILL.md`
- Related: `../14-ui-popup-data-model/SKILL.md`
