# 10-achieve-manager

Status: ACTIVE
AppliesTo: v10

MobilePackage 샘플의 `AchieveManager` 설계 문서다.

---

## Implementation Location (3-path mirror)

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `AchieveManager.cs` | `upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Achieve/` | `Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Achieve/` | `Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Achieve/` |
| `IAchievePlatformAdapter.cs` | 동일 경로 | 동일 경로 | 동일 경로 |

---

## Public API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `RefreshRuntimes()`
- `GetRuntimeState(achievementId)` -> `MissionRuntimeState`
- `ClaimAsync(achievementId, ct)` -> `Task<CommonResult>`
- `Notify(msgType)`
- `Notify(msgType, params object[] args)`
- `Subcribe(EntityId, ACHIEVE_MESSAGE_TYPE, Handler)`
- `SubcribeOnce(EntityId, ACHIEVE_MESSAGE_TYPE, Action<object[]>)`
- `UnSubcribe(EntityId)`
- `UnlockAchievementAsync(achievementId, ct)` -> `Task<CommonResult>`
- `SyncAsync(ct)` -> `Task<CommonResult>`
- `ClearStorage()`

Events:
- `OnRuntimeInitialized`
- `OnRuntimeActive`
- `OnRuntimeProgress`
- `OnRuntimeClaimable`
- `OnRuntimeLevelUp`
- `OnRuntimeRewarded`
- `OnAchievementUnlocked`

---

## Internal Responsibilities

- `TB_ACHIEVE_SOCIAL` + `TB_ACHIEVE_PASS` 기반 runtime 생성/복구
- `ACHIEVE_TYPE` 기반 runtime 타입 분기(`AchieveRuntimeSocial`, `AchieveRuntimePass`)
- `GameMessageManager` 구독 기반 runtime projection 동기화
- `InventoryManager` 구독 기반 PASS runtime(`reqPassId`) WAIT 전이 동기화
- `ACHIEVE_MESSAGE_TYPE` 기반 외부 알림 트리거 publish
- claim 보상 적용 및 level-up 처리
- 플랫폼 adapter 연동 (`IAchievePlatformAdapter.cs`: Apple/Google/Unsupported)

---

## Hard Rules

- 공개 API는 내부 `achievementId`만 사용
- 초기화 시 `achieveId` group 기준 runtime 항상 생성
- `ACHIEVE_SOCIAL`의 `reqMsgId/reqValue`, `ACHIEVE_PASS`의 `reqPassId`/`reqSeasonId` 설정 row는 `WAIT` 시작 후 req 충족 시 `ACTIVE` 전이
- level-up 시 기존 stat 바인딩을 다음 level row로 교체
- level-up 시에도 타입별 req 조건을 재평가해 `WAIT/ACTIVE` 시작 상태를 결정
- `reqPassId`가 있는 PASS runtime은 `InventoryManager` helper 구독으로 Pass 변경 콜백을 받아 재평가한다.
- 플랫폼 unlock 실패는 claim 전체를 롤백하지 않는다(best-effort)
- 플랫폼 unlock은 `ACHIEVE_TYPE.ONCE` runtime에만 수행한다.
- 저장 실패는 claim 실패로 반환한다

---

## Related

- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
- [22-inventory-system/16-inventory-message-trigger](../../22-inventory-system/16-inventory-message-trigger/SKILL.md)
