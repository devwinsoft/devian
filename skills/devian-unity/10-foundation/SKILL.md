# 10-foundation

Status: ACTIVE
AppliesTo: v11
Type: Index / Directory

## Purpose

`com.devian.foundation` 패키지의 역할:

- `devian/10-module`을 Unity(UPM)로 감싸는 계층 (`Devian.Core` asmdef)
- 모듈 타입 Editor (`Devian.Unity.Editor` asmdef — Complex/VersionNumber Drawer)

Unity 런타임 컴포넌트(Pool, Singleton, FSM 등)는 `20-domain-common-system`으로 이동하였다.

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | Overview | 진입점/범위 | `00-overview/SKILL.md` |
| 01 | Policy | 그룹 정책 | `01-policy/SKILL.md` |
| 30 | EditorComplexDrawer | CInt/CFloat/CString PropertyDrawer | `30-editor-complex-drawer/SKILL.md` |
| 31 | VersionNumberDrawer | VersionNumber PropertyDrawer | `31-version-number-drawer/SKILL.md` |

---

## Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Unity 런타임 컴포넌트: `skills/devian-unity/20-domain-common-system/00-overview/SKILL.md`
- UI 관련 컴포넌트: `skills/devian-unity/30-ui-system/SKILL.md`
