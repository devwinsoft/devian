# 10-base-system — Overview

Status: ACTIVE
AppliesTo: v11

`UIPackage`의 기반 시스템 그룹이다.
UIBaseCanvas, UIBasePanel, UIManager, UISettings, UIMessageSystem, UIUtils 등 UI 프레임워크의 핵심 수명주기와 공통 유틸리티를 포함한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-ui-manager](../10-ui-manager/SKILL.md) | `UIManager` — Canvas 수명주기 (AutoSingleton) |
| [11-ui-canvas-system](../11-ui-canvas-system/SKILL.md) | UIBaseCanvas/UIBasePanel/UIBaseContainer 규약 |
| [13-ui-settings](../13-ui-settings/SKILL.md) | `UISettings` — Toast/Popup 통합 전역 설정 asset |
| [30-ui-canvas-id](../30-ui-canvas-id/SKILL.md) | `UI_CANVAS_ID` — UIBaseCanvas prefab 참조 ID (AssetId 패턴) |
| [32-ui-container-id](../32-ui-container-id/SKILL.md) | `UI_CONTAINER_ID` — UIBaseContainer prefab 참조 ID (AssetId 패턴) |
| [33-ui-message-system](../33-ui-message-system/SKILL.md) | `UIMessageSystem` — UI 전용 메시지 시스템 |
| [50-ui-utils](../50-ui-utils/SKILL.md) | `UIUtils` — 공용 static 유틸리티 (좌표 변환, Billboard, Cursor) |

---

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/
```

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
