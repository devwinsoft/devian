# 10-achieve-manager

Status: ACTIVE
AppliesTo: v10

MobileSystem 샘플의 `AchieveManager` 설계 문서다.

---

## Implementation Location (3-path mirror)

| 파일 | UPM (정본) | Packages (sync) | Assets/Samples (import) |
|---|---|---|---|
| `AchieveManager.cs` | `upm/com.devian.samples/Samples~/MobileSystem/Runtime/Achieve/` | `Packages/com.devian.samples/Samples~/MobileSystem/Runtime/Achieve/` | `Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Achieve/` |
| `IAchievePlatformAdapter.cs` | 동일 경로 | 동일 경로 | 동일 경로 |

---

## Public API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `RefreshRuntimes()`
- `GetRuntimeState(achievementId)` -> `MissionRuntimeState`
- `ClaimAsync(achievementId, ct)` -> `Task<CommonResult>`
- `Notify(msgType)`
- `Notify(msgType, params object[] args)`
- `Subcribe(EntityId, ACHIEVE_MESSAGE, Handler)`
- `SubcribeOnce(EntityId, ACHIEVE_MESSAGE, Action<object[]>)`
- `UnSubcribe(EntityId)`
- `UnlockAchievementAsync(achievementId, ct)` -> `Task<CommonResult>`
- `SyncAsync(ct)` -> `Task<CommonResult>`
- `ClearStorage()`

Events:
- `OnRuntimeInitialized`
- `OnRuntimeProgress`
- `OnRuntimeClaimable`
- `OnRuntimeLevelUp`
- `OnRuntimeRewarded`
- `OnAchievementUnlocked`

---

## Internal Responsibilities

- `TB_ACHIEVE` 기반 runtime 생성/복구
- `GameMessageManager` 구독 기반 runtime projection 동기화
- `ACHIEVE_MESSAGE` 기반 외부 알림 트리거 publish
- claim 보상 적용 및 level-up 처리
- 플랫폼 adapter 연동 (`IAchievePlatformAdapter.cs`: Apple/Google/Unsupported)

---

## Hard Rules

- 공개 API는 내부 `achievementId`만 사용
- 초기화 자동 runtime 생성은 `ACHIEVE.achieveType == ACHIEVE_TYPE.DEFAULT` row만 허용
- level-up 시 기존 stat 바인딩을 다음 level row로 교체
- 플랫폼 unlock 실패는 claim 전체를 롤백하지 않는다(best-effort)
- 저장 실패는 claim 실패로 반환한다

---

## Related

- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
