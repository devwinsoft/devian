# 13-achieve-runtime

Status: ACTIVE
AppliesTo: v10

Achieve runtime(`AchieveRuntime`, `AchieveRuntimeFactory`) 규약 문서다.

---

## Implementation Location

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Achieve/AchieveRuntime.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Achieve/AchieveRuntimeFactory.cs`

---

## Runtime Model

- `achieveId`: 업적 그룹 ID
- `messageId`: 현재 level의 `conditionMsgId` key
- `achieveUid`: runtime uid
- `level`: 현재 단계
- `progressValue`: projection value
- `isWaiting`: req 조건 대기 상태
- `isCompleted`: 완료 여부

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

---

## State

- `WAIT`: `!isCompleted && isWaiting`
- `ACTIVE`: `!isCompleted && progressValue < conditionValue`
- `CLAIMABLE`: `!isCompleted && progressValue >= conditionValue`
- `COMPLETED`: `isCompleted`

---

## Factory Rules

- `Create`: 신규 runtime 생성 + (WAIT 또는 ACTIVE) bind
- `Restore`: 저장값 복원 + (WAIT 또는 ACTIVE) bind
- restore 후 progress는 stat reader 값으로 동기화된다.

---

## Related

- [14-achieve-storage](../14-achieve-storage/SKILL.md)
