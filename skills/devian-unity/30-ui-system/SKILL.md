# 30-ui-system

Status: ACTIVE
AppliesTo: v11
Type: Index / Directory

## Purpose

`com.devian.ui` 패키지의 UI 관련 컴포넌트/규약 인덱스 문서이다.

---

## Code Location

UI 컴포넌트 코드는 별도 UPM 패키지로 분리되었다:

```
framework-cs/upm/com.devian.ui/Runtime/
```

주요 파일: `UIManager.cs`, `UICanvas.cs`, `UIFrame.cs`, `Plugins/*` 등.

> **이전 위치:** `com.devian.foundation/Runtime/Unity/UI/` → `com.devian.ui/Runtime/` 로 분리 완료

---

## Package Dependencies (com.devian.ui)

`com.devian.ui`의 `package.json` dependencies:

| 패키지 | 버전 | 비고 |
|--------|------|------|
| `com.devian.foundation` | `0.1.0` | Core + Unity 런타임 기반 |
| `com.devian.domain.common` | `0.1.0` | 필수 (향후 사용 고정) |
| `com.devian.domain.sound` | `0.1.0` | 필수 (향후 사용 고정) |

> **Devian Domain Sound**는 `UIPlugInButton`의 UI 사운드 재생에서 사용한다 (`SOUND_ID`, `TB_SOUND`, `SoundManager`, `SoundChannelType.Ui`).
> **Devian Domain Common**은 `UIPlugInText`의 `ST_TEXT`/`TEXT_ID` 조회에 사용한다.

### Circular Dependency Prevention

`com.devian.foundation`은 `com.devian.ui`를 참조하지 않는다 (순환 의존 방지).
Foundation → UI 하드 참조가 존재하지 않는다.

> **UIManager는 AutoSingleton**이다. `Instance` 접근 시 자동 생성되므로 Bootstrap 부착이 불필요하다.

### Assembly Definitions

| asmdef | name | references | 위치 |
|--------|------|------------|------|
| `Devian.UI.asmdef` | `Devian.UI` | `["Devian.Core", "Devian.Unity", "Devian.Domain.Common", "Devian.Domain.Sound", "Unity.TextMeshPro"]` | `Runtime/` |
| `Devian.UI.Editor.asmdef` | `Devian.UI.Editor` | `["Devian.UI", "Devian.Unity", "Devian.Unity.Editor"]` | `Editor/` |

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | Overview | 진입점/범위 | `00-overview/SKILL.md` |
| 01 | Policy | 문서 작성 정책 (Usage 섹션 금지 등) | `01-policy/SKILL.md` |
| 10 | UIManager | Canvas 수명주기 (AutoSingleton) | `10-ui-manager/SKILL.md` |
| 20 | UICanvasFrames | UICanvas/UIFrame 규약 (overview+policy+ssot 통합) | `20-ui-canvas-frames/SKILL.md` |
| 30 | UIPlugInButton | Button press feedback + UI sound + events + scroll bridge | `30-ui-plugin-button/SKILL.md` |
| 31 | UIPlugInCircleFilter | Collider2D 기반 Raycast filter | `31-ui-plugin-circle-filter/SKILL.md` |
| 32 | UIPlugInNonDrawing | Non-drawing Graphic | `32-ui-plugin-non-drawing/SKILL.md` |
| 33 | UIMessageSystem | UI 전용 메시지 시스템 (UnityEngine.EntityId + UI_MESSAGE) | `33-ui-message-system/SKILL.md` |
| 34 | UIPlugInText | ST_TEXT 바인딩 텍스트 플러그인 (InitOnce + ReloadText) | `34-ui-plugin-text/SKILL.md` |

---

## Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md` (번들 구성/의존 정책)
- Related: `skills/devian-unity/10-foundation/SKILL.md` (기타 Unity 컴포넌트)
