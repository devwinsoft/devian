# 15-ui-component-menu

Status: ACTIVE
AppliesTo: v11

`UIComponentMenuBar` / `UIComponentMenuButton` 수평 단일 선택 메뉴 skill이다.
상세 동작과 구조는 `docs/ui-component-menu-design.md`를 기준으로 한다.

## Core Rules

- `UIComponentMenuBar`가 selection의 source of truth를 가진다.
- `UIComponentMenuButton`는 same-GO `Button.onClick`을 받아 parent bar의 `Select(int index)`를 호출한다.
- `UIComponentMenuBar`는 `RequireComponent(typeof(HorizontalLayoutGroup))`를 가진다.
- `UIComponentMenuButton`는 `RequireComponent(typeof(Button))`를 가진다.
- `UIComponentMenuButton`는 `RequireComponent(typeof(UITransitionPlayer))`를 가진다.
- `UIComponentMenuButton`는 `RequireComponent(typeof(LayoutElement))`를 가진다.
- `UIComponentMenuButton.index`는 serialized `int` field다.
- `UIComponentMenuButton`는 serialized `UI_TRANSITION_PRESET_ID`로 select/deselect transition을 가진다.
- `UIComponentMenuButton`는 `protected virtual void onSelect()` / `onDeselect()` hook으로 subclass visual/state 처리를 확장한다.
- menu 폭/높이 transition은 `LayoutElement.preferredWidth/preferredHeight` 채널을 사용한다.
- menu button은 bar의 direct child로 사용한다.
- duplicate `index`는 invalid다.
- 외부 구독 지점은 `UIComponentMenuBar.OnSelect`다.

## Reference

- Parent: [00-overview](../00-overview/SKILL.md)
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- Button: [11-ui-component-button](../11-ui-component-button/SKILL.md)
- Design: `/Users/maoshy/Documents/Projects/devian/docs/ui-component-menu-design.md`
