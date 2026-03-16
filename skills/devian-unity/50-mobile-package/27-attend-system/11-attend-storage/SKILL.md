# 11-attend-storage

Status: ACTIVE
AppliesTo: v10

`AttendStorage` 저장 모델 규약 문서다.

---

## Ownership

- `AttendManager`가 `AttendStorage`를 소유한다.
- 저장 정본 접근 경계: `AttendManager.Storage`

---

## Storage Model

```csharp
[Serializable]
public sealed class AttendStorage
{
    public int schemaVersion = 2;
    public long cycleStartUtcMs;
    public long lastClaimUtcMs;
    public long lastLoginUtcMs;
    public int nextAttendDay;
    public Dictionary<string, long> claimedAttendUtcMs = new();
}
```

의미:
- `cycleStartUtcMs`: 현재 cycle 시작 서버 시각(UTC ms)
- `lastClaimUtcMs`: 마지막 claim 서버 시각(UTC ms)
- `lastLoginUtcMs`: 마지막 접속 서버 시각(UTC ms)
- `nextAttendDay`: 다음 claim day (`1..8`)
- `claimedAttendUtcMs`: `attendId -> claimUtcMs`

규칙:
- `nextAttendDay == 8`은 7일차 완료 상태(reset 대기)다.
- reset 시 `nextAttendDay=1`, `lastClaimUtcMs=0`, `claimedAttendUtcMs.Clear()`
- `Clear()`는 schema 기본값 + 모든 상태를 초기화한다.
- `AttendRuntime` 리스트는 저장하지 않는다(매 접속 시 서버 시각 기준 재구성).

---

## SaveData Payload

루트 payload에서 Attend 섹션은 `attend` 키를 사용한다.

```json
{
  "version": 17,
  "attend": {
    "schemaVersion": 2,
    "cycleStartUtcMs": 0,
    "lastClaimUtcMs": 0,
    "lastLoginUtcMs": 0,
    "nextAttendDay": 1,
    "claimedAttendUtcMs": {
      "attend_day_001": 1735689600000
    }
  }
}
```

---

## Deserialize Rules

- `AttendStorage.Clear()` 후 복원한다.
- `schemaVersion` 누락 시 1을 기본값으로 사용한다.
- `claimedAttendUtcMs`의 key가 empty면 skip한다.
- value가 음수면 0으로 보정한다.
- `nextAttendDay` 누락 시 기본값 1을 사용한다.
- 스키마 마이그레이션 시 추가 보정 로직은 두지 않는다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/Attend/AttendStorage.cs`
  - `framework-cs/upm/com.devian.samples/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecAttend.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobilePackage/Runtime/...`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/...`

---

## Related

- [10-attend-manager](../10-attend-manager/SKILL.md)
- [03-ssot](../03-ssot/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
