# UIPlugInText

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

ST_TEXT 값을 TMP_Text 컴포넌트에 바인딩하는 UI 플러그인.
`UIManager.messageSystem`를 통해 `UI_MESSAGE.InitOnce`와 `UI_MESSAGE.ReloadText`를 구독하여
초기 세팅 및 언어 변경 시 텍스트를 갱신한다.

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.ui/Runtime/Plugins/UIPlugInText.cs
```

### Serialized Fields

| Field | Type | Description |
|-------|------|-------------|
| `_text` | `TMP_Text` | 텍스트를 표시할 TMP 컴포넌트 |
| `_textId` | `TEXT_ID` | ST_TEXT 조회 키 |

---

## API

### Lifecycle

| Event | Action |
|-------|--------|
| `OnEnable` | `SubcribeOnce(GetEntityId(), UI_MESSAGE.InitOnce, ...)` + `Subcribe(GetEntityId(), UI_MESSAGE.ReloadText, ...)` |
| `OnDisable` | `messageSystem?.UnSubcribe(GetEntityId())` — ownerKey의 모든 핸들러 해제 |

### 내부 메서드

| Method | Description |
|--------|-------------|
| `applyText()` | `ST_TEXT.Get(_textId.Value)` 결과를 `_text.text`에 할당. null/invalid 시 무동작. |

### Handler 동작

- **InitOnce handler**: `Action<object[]>` — `applyText()` 호출 (SubcribeOnce이므로 자동 해제됨)
- **ReloadText handler**: `Handler` — `applyText()` 호출, `return false` (구독 유지)

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIManager.messageSystem` | `com.devian.ui/Runtime/UIManager.cs` |
| `UI_MESSAGE` | `com.devian.ui/Runtime/UIMessageSystem.cs` |
| `UnityEngine.EntityId` (ownerKey via `GetEntityId()`) | Unity 내장 |
| `ST_TEXT` | `com.devian.domain.common/Runtime/Generated/ST_TEXT.g.cs` |
| `TEXT_ID` | `com.devian.domain.common/Runtime/Generated/Common.g.cs` |
| `TMP_Text` | `Unity.TextMeshPro` |

---

## Reference

- **UIMessageSystem**: [33-ui-message-system/SKILL.md](../33-ui-message-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
