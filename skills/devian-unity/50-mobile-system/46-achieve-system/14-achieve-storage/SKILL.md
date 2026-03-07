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
- `stats: Dictionary<string, CBigInt>`

규칙:
- progress source of truth는 `stats[string messageId]`
- `runtimes`는 UI/상태 projection + claim 상태 보존용
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
    "stats": { "<messageId>": { "base": 0, "pow": 0 } },
    "runtimes": [
      {
        "missionId": "...",
        "messageId": "...",
        "achieveUid": 1,
        "level": 1,
        "progressValue": { "base": 0, "pow": 0 },
        "isCompleted": false
      }
    ]
  }
}
```

핵심 규칙:
- achieve runtime 저장 위치는 반드시 `achieve.runtimes`
- mission runtime 저장 위치는 `mission.runtimes`(DAY 전용)

---

## Deserialize Rules

- `AchieveStorage.Clear()` 후 복원
- uid <= 0 항목은 무시
- `nextAchieveUid <= 0`이면 1로 보정
- 누락 키는 안전 기본값 사용
