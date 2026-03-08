# 14-achieve-storage

Status: ACTIVE
AppliesTo: v10

`AchieveStorage` 저장 모델 규약 문서다.

---

## Implementation Location

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/Achieve/AchieveStorage.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecAchieve.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodec.cs`

---

## Ownership

- `AchieveManager`가 `AchieveStorage`를 소유한다.
- 정본 접근 경계: `AchieveManager.Storage`

---

## Storage Model

`AchieveStorage` 필드:
- `schemaVersion`
- `nextAchieveUid`
- `runtimes: Dictionary<int, AchieveRuntime>`

규칙:
- `runtimes`는 업적 진행/claim 상태의 저장 정본이다.
- progress source는 runtime별 `MESSAGE.saveType` 규칙을 따른다.
  - `TOTAL_*`: 외부 저장(`GameMessageStorage`) projection
  - `SESSION_*`: runtime 내부 `progressValue`
- `Clear()`는 schema 기본값 + 모든 컬렉션 초기화

---

## SaveData Payload

루트 payload에서 Achieve 섹션은 `achieve` 키를 사용한다.

```json
{
  "version": 12,
  "achieve": {
    "schemaVersion": 1,
    "nextAchieveUid": 1,
    "runtimes": [
      {
        "achieveId": "...",
        "messageId": "...", // runtime의 conditionMsgId
        "achieveUid": 1,
        "level": 1,
        "progressValue": { "base": 0, "pow": 0 },
        "isWaiting": false,
        "isCompleted": false
      }
    ]
  }
}
```

핵심 규칙:
- achieve runtime 저장 위치는 반드시 `achieve.runtimes`
- period 개념은 없고 `achieve.period` 계층을 사용하지 않는다.

---

## Deserialize Rules

- `AchieveStorage.Clear()` 후 복원
- uid <= 0 항목은 무시
- `nextAchieveUid <= 0`이면 1로 보정
- 누락 키는 안전 기본값 사용
