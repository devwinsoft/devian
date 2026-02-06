# 40-ui-components

Status: ACTIVE
AppliesTo: v10
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

> **Devian Domain Common** 및 **Devian Domain Sound**는 현재 코드에서 즉시 사용하는지 여부와 무관하게,
> 향후 사용을 위해 필수 의존으로 고정한다.

### Circular Dependency Prevention

`com.devian.foundation`은 `com.devian.ui`를 참조하지 않는다 (순환 의존 방지).
`BaseBootstrap.ensureRequiredComponents()`에서 `ensureComponent<UIManager>()` 호출이 삭제되어,
Foundation → UI 하드 참조가 존재하지 않는다.

> **UIManager 자동 보장은 없다.** Bootstrap에서 UIManager를 자동 생성하지 않으므로,
> UIManager 설치/프리팹 구성 책임은 사용자(앱/샘플) 측에 있다.

### Assembly Definitions

| asmdef | name | references | 위치 |
|--------|------|------------|------|
| `Devian.UI.asmdef` | `Devian.UI` | `["Devian.Core", "Devian.Unity"]` | `Runtime/` |
| `Devian.UI.Editor.asmdef` | `Devian.UI.Editor` | `["Devian.UI", "Devian.Unity", "Devian.Unity.Editor"]` | `Editor/` |

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | Overview | 진입점/범위 | `00-overview/skill.md` |
| 01 | Policy | 문서 작성 정책 (Usage 섹션 금지 등) | `01-policy/skill.md` |
| 10 | UIManager | Canvas 수명주기 + UI 입력 보장 (CompoSingleton/Bootstrap) | `10-ui-manager/skill.md` |
| 20 | UICanvasFrames | UICanvas/UIFrame 규약 (overview+policy+ssot 통합) | `20-ui-canvas-frames/skill.md` |
| 30 | UIPlugInButton | Button press feedback + events + scroll bridge | `30-ui-plugin-button/skill.md` |
| 31 | UIPlugInCircleFilter | Collider2D 기반 Raycast filter | `31-ui-plugin-circle-filter/skill.md` |
| 32 | UIPlugInNonDrawing | Non-drawing Graphic | `32-ui-plugin-non-drawing/skill.md` |

---

## Reference

- Parent: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md` (번들 구성/의존 정책)
- Related: `skills/devian-unity/30-unity-components/skill.md` (기타 Unity 컴포넌트)
