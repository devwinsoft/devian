# 12-ui-component-text

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

ST_GAME_TEXT 값을 TMP_Text 컴포넌트에 바인딩하는 UI 플러그인.
`UIComponentBase` 초기화 시 즉시 텍스트를 적용하고,
`UIManager.messageSystem`의 `UI_MESSAGE.ReloadText`를 구독하여 언어 변경 시 텍스트를 갱신한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentText.cs
```

### Serialized Fields

| Field | Type | Description |
|-------|------|-------------|
| `_text` | `TMP_Text` | 텍스트를 표시할 TMP 컴포넌트 |
| `_textId` | `GAME_TEXT_ID` | ST_GAME_TEXT 조회 키 |

---

## API

### Lifecycle

| Event | Action |
|-------|--------|
| `onInit(Canvas)` | `applyText()` 호출 + `Subcribe(GetEntityId(), UI_MESSAGE.ReloadText, ...)` |
| `onDestroy()` | `messageSystem?.UnSubcribe(GetEntityId())` — ownerKey의 모든 핸들러 해제 |

### 내부 메서드

| Method | Description |
|--------|-------------|
| `applyText()` | `ST_GAME_TEXT.Get(_textId.Value)` 결과를 `_text.text`에 할당. null/invalid 시 무동작. |

### Handler 동작

- **ReloadText handler**: `Handler` — `applyText()` 호출, `return false` (구독 유지)

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIManager.messageSystem` | `com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIManager.cs` |
| `UI_MESSAGE` | `com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIMessageSystem.cs` |
| `UnityEngine.EntityId` (ownerKey via `GetEntityId()`) | Unity 내장 |
| `ST_GAME_TEXT` | `com.devian.foundation/Samples~/GamePackage/Runtime/Generated/ST_GAME_TEXT.g.cs` |
| `GAME_TEXT_ID` | `com.devian.foundation/Samples~/GamePackage/Runtime/Generated/Game.g.cs` |
| `TMP_Text` | `Unity.TextMeshPro` |

---

## Reference

- Parent: `../00-overview/SKILL.md`
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- Index: `skills/devian-unity/23-ui-package/SKILL.md`
- **UIMessageSystem**: [33-ui-message-system/SKILL.md](../../10-base-system/33-ui-message-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../../10-base-system/10-ui-manager/SKILL.md)
