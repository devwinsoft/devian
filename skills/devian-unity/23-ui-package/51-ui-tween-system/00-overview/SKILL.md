# 51-ui-tween-system — Overview

Status: ACTIVE
AppliesTo: v1

UITweenSystem은 UIPackage의 UI 전용 최소 tween / transition 계층이다.
목표는 `UIToastFrame` show/hide와 향후 `UIPanel` / `UIPopup` show/hide 연출을 안정적으로 실행하는 것이다.
자동 panel show/hide hook뿐 아니라, game event 시점의 manual play도 지원한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | UITween 모듈 경계와 금지 사항 |
| [10-ui-tween-runner](../10-ui-tween-runner/SKILL.md) | tween 실행 엔진 |
| [11-ui-tween-handle](../11-ui-tween-handle/SKILL.md) | 실행 중 tween 참조 / cancel 상태 |
| [12-ui-tween-sequence](../12-ui-tween-sequence/SKILL.md) | Append / Join sequence 규약 |
| [13-ui-transition-preset](../13-ui-transition-preset/SKILL.md) | alpha / position / scale 연출 데이터 |
| [14-ui-transition-player](../14-ui-transition-player/SKILL.md) | preset을 실제 UI 대상에 적용하는 플레이어 |

---

## Scope

### Includes
- `CanvasGroup.alpha`
- `RectTransform.anchoredPosition`
- `Transform.localScale`
- `duration`, `delay`, `easing`
- `cancel`
- `onComplete`
- `sequence (Append / Join)`
- `UITransitionPresetAsset` + `UI_TRANSITION_PRESET_ID` 기반 preset 선택
- `UIPanel.Show()` / `UIPanel.Hide()`와 manual `Play(...)` 양쪽 진입점

### Excludes
- 범용 property tween
- reflection 기반 tween
- path animation
- editor tooling
- gameplay object tween

---

## Related

- Parent: `../../SKILL.md`
- Parent Overview: `../../00-overview/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
