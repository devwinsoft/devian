# 52-ui-toast-system — Policy

Status: ACTIVE
AppliesTo: v11

---

## Canvas Ownership

- `UIManager.EnsureCanvas`는 toast bootstrap에 사용하지 않는다.
- toast canvas는 `MobileApplication`이 생성한다 (`BundlePool.Spawn<UIToastCanvas>`).
- `UIToastService`는 existing canvas만 조회한다 (`UIToastCanvas.Instance` → `FindAnyObjectByType`).

## Text Boundary

- toast system은 `string Message`만 받는다.
- localization 해석은 호출부에서 먼저 수행한다.
- `TEXT_ID` 등 localization key를 toast system 내부에서 해석하지 않는다.

## Non-Blocking

- toast는 입력을 막지 않는다.
- popup blocker / dim / modal flow를 사용하지 않는다.
- `UIToastFrame`은 spawn 즉시 `CanvasGroup.blocksRaycasts = false`와 child `Graphic.raycastTarget = false`를 강제한다 (`ApplyNonBlocking()`).

## Tween Boundary

- toast layout root는 `UIToastGroup`이 소유한다.
- show/hide tween은 `UIToastFrame` 내부 `UITransitionPlayer`가 담당한다.
- tween 대상은 frame root가 아니라 prefab 내부 `VisualRoot`를 권장한다.
- `UIToastGroup`은 tween을 직접 실행하지 않는다.

## Pool Cleanup

- `UIToastFrame` cleanup은 `OnPoolSpawned()` / `OnPoolDespawned()`에서 수행한다.
- slot release(`BundlePool.Despawn`)는 lifetime 종료가 아니라 **hide tween 완료** 시점에 수행한다.
- `UIToastGroup.OnFrameHidden()` 콜백이 Despawn을 호출한다.

## Group Fallback

- 요청된 `groupId`에 해당하는 group이 없으면 default group(`"System"`)으로 fallback한다.
- default group도 없으면 `CreateDefaultConfig()`로 즉시 생성한다.

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
