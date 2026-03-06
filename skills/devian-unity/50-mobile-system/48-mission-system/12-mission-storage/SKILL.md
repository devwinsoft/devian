# 12-mission-storage

Status: ACTIVE  
AppliesTo: v10  
Type: Design / SSOT

## Purpose

`MissionStorage`의 저장 구조와 복구 규칙 정본이다.

---

## Schema

```csharp
public sealed class MissionStorage
{
    public int schemaVersion; // default: 2
    public long dailyMissionStartUtcMs;
    public MissionClockSnapshot clockSnapshot;
    public long clockReceivedAtClientUtcMs;
    public int nextMissionUid;
    public Dictionary<int, MissionRuntimeBase> runtimes;
    public Dictionary<string, CBigInt> stats; // key: missionStatId
}
```

핵심:

- `stats[string missionStatId]`가 achievement progress 정본이다.
- `schemaVersion` 기본값은 2를 사용한다.
- legacy fallback/bridge는 두지 않는다.

---

## Runtime Stored Shape

```csharp
public abstract class MissionRuntimeBase
{
    public MISSION_TYPE missionType;
    public string missionId;
    public string missionStatId;
    public string periodKey;
    public int missionUid;
    public CBigInt progressValue;
    public bool isCompleted;
}
```

타입별 저장 규칙:

- `DAY`:
  - `periodKey` 저장
  - `index` 저장
  - `progressValue` 저장
- `ACHIEVE`:
  - `level` 저장
  - `periodKey`는 저장하지 않음(복원 시 `"once"`)
  - `progressValue`는 저장 정본 아님(`stats` 사용)

---

## Persistence Rules

- `DAY progress`는 runtime 로컬 값으로 저장/복원한다.
- `ACHIEVE progress`는 `stats[missionStatId]`를 저장/복원한다.
- `ACHIEVE runtime.progressValue`는 deserialize 시 0으로 시작하고 bind 단계에서 stats reader로 동기화된다.
- `rewardGroupId` 등 definition 데이터는 runtime에 저장하지 않는다.

---

## Initialize / Recovery

1. storage 로드
2. clock/anchor 보정
3. scheduler rebuild/prune
4. runtime bind 시:
   - daily는 runtime 저장 progress 사용
   - achieve는 stats reader 사용

---

## Hard Rules

- MissionStorage mutation은 MissionManager 경로만 허용
- `stats` key는 반드시 `missionStatId` string
- `ACHIEVE periodKey`는 payload source가 아니며 항상 `"once"`
- 구포맷(`missionKind`, achieve legacy progress) 호환 로직 금지

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
