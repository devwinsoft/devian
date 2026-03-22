# 15-ui-popup-frame-editor — Popup Frame Editor UX

Status: ACTIVE
AppliesTo: v1

## Purpose

`UISettings.PopupFrameMappings` 편집 UX를 담당한다.

- `UI_POPUP_FRAME_ID` drawer/selector를 제공한다
- `FrameTypeName` raw string 입력을 숨긴다
- concrete `UIPopupFrameBase` type selector를 제공한다
- `UI_POPUP_FRAME_ID` search dir의 실제 prefab을 스캔해서 auto fill 한다

## Target Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UI_POPUP_FRAME_ID_Drawer.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameIdSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameTypeSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameMapEntry_Drawer.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameEditorUtility.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UISettingsEditor.cs
```

## UX Goal

- `Frame Id`는 `Select` 버튼으로 prefab을 고른다
- `Frame Type`은 concrete popup frame type selector로 고른다
- `Auto Fill`은 이름 규칙이 아니라 실제 prefab의 popup frame component type을 읽어서 채운다
- `UISettings` inspector에서 missing mappings를 한 번에 채울 수 있다

## Rules

- `FrameId`는 `UI_POPUP_FRAME_ID` property drawer를 재사용한다
- `FrameTypeName` 저장값은 runtime lookup용 문자열을 유지한다
- selector 목록에는 abstract type과 open generic base를 포함하지 않는다
- auto fill은 `UISettings.GetSearchDir("UI_POPUP_FRAME_ID")` 기준으로 prefab을 스캔한다
- auto fill은 prefab 이름 추정 규칙을 사용하지 않는다
- auto fill은 prefab의 실제 pool type이 `UIPopupFrameBase` 파생형일 때만 매핑한다
- 동일 frame type에 prefab이 여러 개면 auto fill 하지 않는다
- 동일 prefab name이 여러 경로에 있으면 auto fill 하지 않는다
- asset-level auto fill은 기존 mapping을 덮어쓰지 않고 missing 항목만 보완한다

## Current Behavior

- `UIPopupFrameMapEntry_Drawer`
  - `Frame Type` readonly label
  - `Select Type` button
  - `Frame Id` field
  - `Auto Fill` button
- `UISettingsEditor`
  - `Auto Fill Missing Popup Mappings` button
- `UIPopupFrameEditorUtility`
  - popup prefab scan
  - type resolve
  - entry auto fill
  - settings-level auto fill

## Dependency

- Parent: `../00-overview/SKILL.md`
- Related: `../16-ui-popup-settings/SKILL.md`
- Related: `../14-ui-popup-data-model/SKILL.md`
