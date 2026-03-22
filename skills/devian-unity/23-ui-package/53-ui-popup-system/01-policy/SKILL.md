# 53-ui-popup-system — Policy

Status: ACTIVE
AppliesTo: v1

## Hard Rules

- popup canvas bootstrap은 `MobileApplication`이 담당한다.
- `UIManager.EnsureCanvas`는 popup bootstrap에 사용하지 않는다.
- `UIPopupManager`는 `AutoSingleton<UIPopupManager>`다.
- popup은 항상 stack으로 관리한다. 단일 popup 구조를 두지 않는다.
- v1 public close API는 `CloseTop()` / `CloseAll()`만 둔다.
- `CloseById` 같은 중간 popup close API는 v1에서 지원하지 않는다.
- top popup만 입력 가능하다.
- dim 상태는 항상 top popup config 기준으로만 계산한다.
- dim은 panel 아래 단일 shared layer로 유지한다.
- dim click은 `UIPopupDim : IPointerClickHandler`가 처리한다.
- `Button` / `EventTrigger`는 dim click 처리에 사용하지 않는다.
- `FocusIfOpened`는 stack `remove -> push`로만 처리한다.
- `UITransitionPlayer`는 executor-only 유지한다.
- open / close preset 의미는 `UIPopupFrame` 또는 concrete popup frame이 가진다.
- `UIPopupFrame`은 `Opening / Opened / Closing` 상태를 가진다.
- `Closing` 중 추가 close 요청은 무시한다.
- `CloseAll()`은 `ForceClosed` reason으로 transition 없이 즉시 처리한다.
- popup 내부에 business logic를 두지 않는다.
- 구형 popup 구조 대체 경로는 만들지 않는다.

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
