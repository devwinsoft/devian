---
name: game-message-storage
description: Use this skill when implementing or refactoring GameMessageStorage and SaveData JSON codec flow for message.stats in MobileSystem.
---

# 14-game-message-storage

Status: ACTIVE
AppliesTo: v10
Type: Design / SSOT

## Purpose

`GameMessageStorage`의 저장 모델과 payload 규약 정본이다.

---

## Implementation Location (3-path mirror)

- UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/GameMessage/GameMessageStorage.cs`
- UPM Codec: `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecMessage.cs`
- Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/GameMessage/GameMessageStorage.cs`
- Packages Codec (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecMessage.cs`
- Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/GameMessage/GameMessageStorage.cs`
- Assets Codec (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Runtime/SaveData/JsonCodec/SaveDataJsonCodecMessage.cs`

---

## Ownership

- `GameMessageManager`가 `GameMessageStorage`를 소유한다.
- stat 저장 접근 정본: `GameMessageManager.Storage`

---

## Storage Model

`GameMessageStorage` 필드:
- `schemaVersion`
- `stats: Dictionary<string, CBigInt>`

규칙:
- key는 반드시 `messageId` string
- value는 `MESSAGE.saveType` 규약에 따라 누적된 값
- `Clear()`는 schema 기본값 복원 + 컬렉션 초기화

---

## SaveData Payload

루트 payload에서 message 섹션은 `message` 키를 사용한다.

```json
{
  "version": 13,
  "message": {
    "schemaVersion": 1,
    "stats": { "<messageId>": { "base": 0, "pow": 0 } }
  }
}
```

핵심 규칙:
- `mission.stats`는 더 이상 write하지 않는다.
- mission/achieve runtime은 `messageId` 필드를 사용한다.

---

## Migration Rules

- v12 이하 payload load 시:
  - legacy `mission.stats`를 `message.stats`로 이동한다.
  - runtime의 legacy 필드 `missionStatId`는 `messageId` fallback으로 읽는다.
- v13 write 시:
  - `message.stats`만 기록한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-game-message-manager](../10-game-message-manager/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
- [48-mission-system/12-mission-storage](../../48-mission-system/12-mission-storage/SKILL.md)
