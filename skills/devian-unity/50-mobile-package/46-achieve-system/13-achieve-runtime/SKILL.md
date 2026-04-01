# 13-achieve-runtime

Status: ACTIVE
AppliesTo: v10

Achieve runtime(`AchieveRuntimeBase`, `AchieveRuntimeSocial`, `AchieveRuntimePass`, `AchieveRuntimeFactory`) 규약 문서다.

---

## Implementation Location

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Achieve/AchieveRuntime.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Achieve/AchieveRuntimeFactory.cs`

---

## Runtime Model

- `runtimeType`: `ACHIEVE_TYPE` (`ONCE`, `PASS`)
- `achieve_id`: 업적 그룹 ID
- `achieveUid`: runtime uid
- `level`: 현재 단계
- `index`: UI 정렬 인덱스(`order_num - 1`)
- `progressValue`: projection value
- `state`: `MissionRuntimeState` (`WAIT` / `ACTIVE` / `COMPLETED`)

정본:
- `TOTAL_*` saveType은 `GameMessageStorage` 값으로 projection
- `SESSION_*` saveType은 runtime 내부 `progressValue`로 유지

---

## Binding Rules

- `Bind`는 현재 row 기준 바인딩을 교체하고 stat reader를 연결한다.
- `BindWaiting`은 req 대기 상태로 전환하며 condition projection을 비활성화한다.
- `LevelUp`은 다음 row 기준으로 stat 바인딩을 교체한다.
- `LevelUpToWaiting`은 다음 row를 req 대기 상태로 전환한다.
- `LevelUp`에서 `SESSION_SUM`은 progress를 0으로 리셋한다.
- `Detach`는 콜백/reader 참조를 해제한다.
- period 개념은 없다.
- WAIT 진입 사유는 runtime 타입별 req 조건이다.
  - `ONCE`: `req_msg_id/req_value`
  - `PASS`: `req_pass_id` / `req_season_id`
- `req_pass_id`가 있는 `PASS` runtime은 Pass 소유 조건 충족 시 `ACTIVE`로 전이한다.

---

## State

- `WAIT`: `state == MissionRuntimeState.WAIT`
- `ACTIVE`: `state == MissionRuntimeState.ACTIVE && !IsClaimable`
- `CLAIMABLE`: `state == MissionRuntimeState.ACTIVE && IsClaimable`
- `COMPLETED`: `state == MissionRuntimeState.COMPLETED`

---

## Factory Rules

- `Create`: 신규 runtime 생성 + (WAIT 또는 ACTIVE) bind
- `Restore`: 저장값 복원 + (WAIT 또는 ACTIVE) bind
- `AchieveType`에 맞는 runtime 클래스를 생성한다.
  - `ONCE` -> `AchieveRuntimeSocial`
  - `PASS` -> `AchieveRuntimePass`
- restore 후 progress는 stat reader 값으로 동기화된다.

---

## Related

- [14-achieve-storage](../14-achieve-storage/SKILL.md)
