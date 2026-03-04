# 12-mission-storage


Status: ACTIVE
AppliesTo: v10
Type: Design / SSOT


## Purpose

MissionManager가 소유하는 `MissionStorage`의 로컬 저장 구조와 복구 규칙을 정의한다.

- Mission 진행도/완료 상태의 저장 정본이다.
- timed mission은 destructive reset 대신 period-scoped key를 저장한다.
- timed mission의 day 경계는 `dailyMissionStartUtcMs` anchor를 기준으로 계산한다.


---


## Ownership

- `MissionManager`는 `CompoSingleton<MissionManager>`다.
- `MissionManager`가 `MissionStorage`를 단일 소유한다.
- `MissionScheduler`는 `MissionStorage`를 새로 소유하지 않으며, `MissionManager.Storage`를 참조해 lifetime mutation만 수행한다.
- `SaveDataManager`는 `MissionStorage`를 포함한 root save payload를 직렬화/역직렬화한다.
- Mission 도메인 mutation은 `MissionManager`만 수행한다. codec은 저장/복원만 담당한다.


---


## Class Boundary

```text
MissionManager : CompoSingleton<MissionManager>
│
├── Storage : MissionStorage
├── Scheduler : MissionScheduler
├── InitializeAsync(ct)
│   ├── load storage
│   ├── refresh MissionClockSnapshot
│   └── scheduler rebuild/prune
└── ClaimAsync(missionType, missionId, ...)
    ├── apply reward locally
    ├── mark runtime isCompleted = true
    ├── save local/cloud immediately
    └── local save failure -> fatal error (TODO)
```


---


## MissionStorage Schema

```csharp
public sealed class MissionStorage
{
    public int schemaVersion;
    public long dailyMissionStartUtcMs;
    public MissionClockSnapshot? clockSnapshot;
    public long clockReceivedAtClientUtcMs;
    public int nextMissionUid;
    public Dictionary<int, MissionRuntimeBase> runtimes;
}
```

- `schemaVersion`: mission storage schema version
- `dailyMissionStartUtcMs`: 첫 login 성공 시의 서버 시각. daily period anchor
- `clockSnapshot`: 마지막으로 수신한 서버 clock snapshot (`serverNowUtcMs`, `minVersion`, `currentVersion` 포함)
- `clockReceivedAtClientUtcMs`: snapshot 수신 시점의 client utc ms
- `nextMissionUid`: MissionScheduler가 새 runtime에 발급할 다음 UID
- `runtimes`: `missionUid(int) -> MissionRuntimeBase`

### MissionRuntimeBase / MissionRuntimeDaily / MissionRuntimeAchieve

```csharp
public abstract class MissionRuntimeBase
{
    public MISSION_TYPE missionType;
    public string missionId = "";
    public string periodKey = "";
    public int missionUid;
    public CBigInt progressValue;
    public bool isCompleted;
}

public sealed class MissionRuntimeDaily : MissionRuntimeBase
{
    public int index;
}

public sealed class MissionRuntimeAchieve : MissionRuntimeBase
{
    public int level;
}
```

규칙:
- `missionUid`는 MissionScheduler가 발급하는 증가형 `int`다.
- `missionUid`는 `nextMissionUid++`를 기본으로 사용하되, 현재 `runtimes`에 이미 존재하는 UID는 건너뛴다.
- `nextMissionUid`의 초기값은 `1`이다.
- `periodKey`는 현재 runtime의 claim/reset 구간 메타데이터를 복구하기 위한 값이다.
- daily runtime은 0-based `Index`를 저장/복구한다.
- achievement runtime의 `Index`는 현재 row의 `orderNum - 1` 계산값이다.
- runtime 생성 = 해당 미션 definition의 runtime 시작이다.
- achievement는 `missionId`가 그룹 ID이고 `level`이 실제 단계다.
- achievement definition의 `missionId + level` 유일성은 data layer에서 보장한다.
- `progressValue`는 같은 runtime 생명주기 안에서 유지되는 누적값이다.
- `isCompleted == true`는 claim 완료 상태를 의미한다.
- `progressValue >= conditionValue && isCompleted == false`이면 `CLAIMABLE` 상태로 해석한다.
- `conditionType` / `conditionOp` / `conditionValue`는 row가 가진 정본이고, runtime은 저장값만 보관한다.
- `claimed` 같은 중복 bool은 두지 않는다.
- codec은 concrete runtime type을 보존해야 하며, restore 시 matching subclass로 복원해야 한다.


## Persistence Rules

- daily는 init/reset cycle마다 기존 daily runtime set을 정리하고 새 runtime set을 만든다.
- 새 period로 진입하면 daily runtime이 현재 구간 기준으로 reset 된다.
- achievement는 `once` scope를 사용한다.
- achievement는 수동 claim 모델이다.
- achievement `CLAIMABLE` runtime은 claim 전까지 유지한다.
- achievement `ClaimAsync()` 성공 시 다음 level row가 있으면 같은 runtime이 level up 한다.
- 단, 다음 level row가 없으면 현재 completed runtime은 유지한다.
- achievement level up 시 `missionUid`는 유지하고, `level` / `isCompleted`만 다음 row 기준으로 갱신한다.
- achievement level up 시 `progressValue`는 현재 값을 그대로 유지한다.
- daily period key는 `dailyMissionStartUtcMs` anchor에서 24시간 단위 index로 계산한다.
- daily는 현재 cycle에서 선택된 최대 5개 runtime만 유지한다.
- achievement는 active group별 runtime 1개만 유지한다.
- MissionStorage는 `missionType`, `missionId` 같은 runtime 식별/복구 정보는 저장한다.
- MissionStorage는 MissionRuntimeBase 계열 concrete runtime 객체를 직접 저장한다.
- 다만 row 전체를 정본으로 저장하지 않는다. 컨텐츠 정본은 항상 `MISSION_*` 테이블이다.
- `rewardGroupId`는 runtime에 저장하지 않는다. claim 시 테이블에서 직접 조회한다.


---


## Load / Save / Recovery

### Initialize

1. 저장된 `MissionStorage`를 로드한다.
2. `MissionClockSnapshot`을 갱신하거나 cached snapshot을 사용한다.
3. `dailyMissionStartUtcMs`가 0이면 첫 successful sync의 `serverNowUtcMs`로 초기화한다.
4. 현재 sync 시각과 `dailyMissionStartUtcMs`의 차이가 7일을 초과하면 `dailyMissionStartUtcMs`를 현재 `serverNowUtcMs`로 재설정하고 daily 데이터를 정리한다.
5. 저장된 runtime이 있으면 저장된 `progressValue` / `isCompleted`를 사용해 restore 한다.
   - achievement runtime은 저장된 `level` / `progressValue` / `isCompleted`를 그대로 restore 한다.
6. daily runtime의 저장된 `periodKey`가 현재 `dailyKey`와 다르면 같은 runtime을 현재 구간 기준으로 reset 한다.
7. 저장된 runtime이 없으면 현재 scope에서 필요한 MissionRuntime을 새로 만든다.
8. 현재 period 기준으로 expired runtime을 prune 한다.


### Clear

- `MissionStorage.Clear()`는 `dailyMissionStartUtcMs`, `clockSnapshot`, `runtimes`를 모두 비운다.
- `SaveDataManager.ClearGameState()`는 MissionStorage도 함께 초기화해야 한다.


---


## Prune Rules

- daily:
  - stale period의 daily runtime을 정리한다
- achievement:
  - 완료된 runtime을 유지한다
- prune 시점:
  - `InitializeAsync`
  - `RefreshClockAsync` 성공 직후


---


## SaveData Integration

MissionStorage는 `21-savedata-system` 루트 save에 별도 섹션으로 들어가야 한다.

권장 root JSON 예시:

```json
{
  "version": 10,
  "inventory": {},
  "purchase": {},
  "account": {},
  "mission": {}
}
```

정본 규칙:
- root section 이름은 `mission`
- root codec이 `MissionStorage`를 serialize/deserialize 한다
- MissionStorage codec은 domain 판정을 하지 않는다
- 로드 후 런타임 재적용은 `MissionManager`가 담당한다


---


## Hard Rules

- `isCompleted == true`인 runtime에 대해 reward를 재지급하지 않는다.
- `runtimes`는 `missionUid(int)`를 key로 사용한다.
- `MissionStorage`는 서버 절대 시간을 만들지 않는다. 서버 시간은 `MissionClockSnapshot`에서만 온다.
- timed mission claim/period 판정은 `dailyMissionStartUtcMs`와 마지막으로 동기화한 `MissionClockSnapshot` 기준 추정 서버 시각으로 수행한다.
- `MissionStorage`는 `MissionManager` 외부에서 직접 mutate하지 않는다.


---


## Related

- [03-ssot](../03-ssot/SKILL.md) — missionUid / grantId / backend contract 정본
- [09-ssot-operations](../09-ssot-operations/SKILL.md) — 운영/복구 시나리오
- [10-mission-manager](../10-mission-manager/SKILL.md) — MissionManager 설계
- [13-mission-runtime](../13-mission-runtime/SKILL.md) — MissionRuntime started/completed/reset 정본
- [43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md) — root save codec 연동
