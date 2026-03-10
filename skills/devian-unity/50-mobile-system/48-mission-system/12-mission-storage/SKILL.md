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
    public long periodMissionStartUtcMs;
    public int nextMissionUid;
    public Dictionary<int, MissionRuntimeBase> runtimes;
}
```

핵심:

- MissionStorage는 runtime 상태만 저장한다.
- stat 누적 정본은 [45-meta-message-system/14-meta-message-storage](../../45-meta-message-system/14-meta-message-storage/SKILL.md)의 `message.stats`다.
- `schemaVersion` 기본값은 2를 사용한다.

---

## Runtime Stored Shape

```csharp
public abstract class MissionRuntimeBase
{
    public string missionId;
    public string conditionMsgId;
    public string periodKey;
    public int missionUid;
    public int index;
    public CBigInt progressValue;
    public bool isWaiting;
    public bool isCompleted;
}
```

타입별 저장 규칙:

- `MissionRuntimeDaily`:
  - `periodKey` 저장
  - `index` 저장
  - `progressValue` 저장
- `MissionRuntimePeriod`:
  - `periodKey` 저장
  - `index` 저장
  - `isWaiting` 저장
  - `progressValue` 저장

---

## Persistence Rules

- `DAILY/PERIOD progress`는 runtime 로컬 값으로 저장/복원한다.
- stat 누적 값은 mission payload에 저장하지 않는다.
- runtime의 `conditionMsgId`는 정의 테이블 `MESSAGE_META.messageId`를 참조한다.
- `rewardGroupId` 등 definition 데이터는 runtime에 저장하지 않는다.

---

## Initialize / Recovery

1. storage 로드
2. anchor 보정 (현재 서버시각: `RemoteConfigManager`)
3. scheduler rebuild/prune
4. runtime bind 시 저장 progress 복원
5. period runtime은 WAIT 상태를 복원한 뒤 day 규칙으로 재활성화

---

## Hard Rules

- MissionStorage mutation은 MissionManager 경로만 허용
- Mission payload는 `stats`를 소유하지 않는다.
- message stat 마이그레이션은 SaveData codec에서 처리한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [13-mission-runtime](../13-mission-runtime/SKILL.md)
- [45-meta-message-system/14-meta-message-storage](../../45-meta-message-system/14-meta-message-storage/SKILL.md)
- [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
