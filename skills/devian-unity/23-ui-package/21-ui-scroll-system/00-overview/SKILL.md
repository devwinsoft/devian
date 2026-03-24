# 21-ui-scroll-system — Overview

Status: ACTIVE
AppliesTo: v11

`UIPackage`의 scroll 전용 시스템 그룹이다.
`UIScrollContainer` owner, scroll section frame, scroll cell ID 규약을 포함한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-ui-scroll-container](../10-ui-scroll-container/SKILL.md) | `UIScrollContainer` — scroll owner + logical row virtualization |
| [11-ui-scroll-frame-grid](../11-ui-scroll-frame-grid/SKILL.md) | `UIScrollGridFrame` / `UIScrollGridCell` — grid section renderer |
| [12-ui-scroll-frame-simple](../12-ui-scroll-frame-simple/SKILL.md) | `UIScrollSimpleFrame` — fixed 1-row section |
| [21-ui-scroll-cell-id](../21-ui-scroll-cell-id/SKILL.md) | `UI_SCROLL_CELL_ID` — scroll cell prefab asset ID |

---

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/
```

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
