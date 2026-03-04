# 31-version-number-drawer

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

`Devian.VersionNumber` 필드를 Unity Inspector에서 `Major.Minor.Patch` 3개 IntField로 편집할 수 있는 PropertyDrawer를 제공한다.

---

## Hard Rules

1. **Editor-only**: `UnityEditor` 참조이므로 Runtime 어셈블리에 포함하지 않는다.
2. **음수 clamp**: Inspector 입력 값이 음수이면 0으로 clamp.
3. **namespace**: `Devian` (30-editor-complex-drawer와 동일)

---

## Inspector 레이아웃

한 줄에 Label + 3개 IntField 배치:

```
[Label       ] [Major] [Minor] [Patch]
```

---

## Files (SSOT)

```
framework-cs/upm/com.devian.foundation/Editor/VersionNumber/VersionNumberPropertyDrawer.cs
```

---

## Reference

- VersionNumber 타입: `skills/devian/10-module/20-core/14-version-number/SKILL.md`
- Drawer 패턴 참고: `skills/devian-unity/10-foundation/30-editor-complex-drawer/SKILL.md`
- Parent: `skills/devian-unity/10-foundation/SKILL.md`
