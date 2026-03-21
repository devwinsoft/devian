# 51-ui-tween-system — Overview

Status: ACTIVE
AppliesTo: v2

UITweenSystem은 UIPackage의 UI 전용 최소 tween / transition 계층이다.
핵심은 channel별 timeline을 compile해서, 매 프레임 단일 result를 평가하고 적용하는 구조다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | UITween 모듈 경계와 금지 사항 |
| [10-ui-tween-runner](../10-ui-tween-runner/SKILL.md) | compiled transition 실행 엔진 |
| [11-ui-tween-handle](../11-ui-tween-handle/SKILL.md) | 실행 중 tween 참조 / cancel 상태 |
| [12-ui-tween-sequence](../12-ui-tween-sequence/SKILL.md) | Append / Join를 단일 timeline으로 flatten하는 규약 |
| [13-ui-transition-preset](../13-ui-transition-preset/SKILL.md) | channel clip 기반 authoring data |
| [14-ui-transition-player](../14-ui-transition-player/SKILL.md) | compiled result를 실제 UI 대상에 적용하는 executor |

---

## Scope

### Includes
- `CanvasGroup.alpha`
- `RectTransform.anchoredPosition`
- `Transform.localScale`
- channel별 `StartTime`, `Duration`, `Ease`
- `cancel`
- `onComplete`
- `sequence (Append / Join)`
- `UITransitionPresetAsset` + `UI_TRANSITION_PRESET_ID` 기반 preset 선택
- compile -> snapshot -> frame result -> apply 실행 구조
- manual `Play(...)`와 panel/frame integration

### Excludes
- 범용 property tween
- reflection 기반 tween
- path animation
- editor tooling
- gameplay object tween
- 구형 preset 대체 경로

---

## Related

- Parent: `../../SKILL.md`
- Parent Overview: `../../00-overview/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
