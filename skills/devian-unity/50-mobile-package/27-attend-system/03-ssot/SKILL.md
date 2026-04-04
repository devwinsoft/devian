# 03-ssot — 27-attend-system (SSOT)

Status: ACTIVE
AppliesTo: v10

## 이 문서가 정본이다 (SSOT)

출석 시스템의 정본은 이 문서다.

- `ATTEND` 테이블 스키마
- row 필터/정렬 규칙
- cycle/day 계산 규칙
- claim 가능/처리 규칙
- 저장 스키마(AttendStorage)

---

## A) ATTEND Table Schema (정본)

정본 입력:
- `input/Domains/Game/MetaTable.xlsx`
- sheet: `ATTEND`

| 필드 | 타입 | 옵션 | 설명 |
|---|---|---|---|
| `attend_id` | string | pk | 출석 항목 ID |
| `is_active` | bool | | 운영 활성 토글 |
| `day` | int | | UI/운영 day (1부터 시작) |
| `reward_group_id` | string | | 지급할 보상 그룹 ID (`TB_REWARD` group key) |

런타임에서는 `Devian.Domain.Game.ATTEND` row와 `TB_ATTEND` container를 사용한다.

---

## B) Runtime Row Selection Rules (정본)

`activeRows` 계산:
1. `TB_ATTEND.GetAll()` 조회
2. `Is_active == true`만 포함
3. `day >= 1 && day <= 7`만 포함
4. `reward_group_id`가 null/empty/whitespace가 아닌 row만 포함
5. `day ASC`, tie-break `attend_id ASC`로 정렬

`day -> row` 매핑:
- 같은 day가 여러 개인 경우 정렬 후 첫 row를 운영 row로 사용한다.
- 출석 보상 day 범위는 `1..7` 고정이다.

`attend runtime` 생성:
- 런타임은 day `1..7` 고정 슬롯으로 항상 7개를 생성한다.
- 각 슬롯은 `day -> row` 매핑 결과를 참조한다.
- 해당 day row가 없으면 `isConfigured=false` 슬롯으로 유지한다(보정 없음).

---

## C) Cycle / Day Rules (정본)

기본 상수:
- `DayMs = 86_400_000` (UTC 기준 24h)
- `MaxAttendDay = 7`
- `ResetAfterClaimMs = 259_200_000` (72h)

핵심 상태:
- `AttendStorage.nextAttendDay` (다음 claim day, 범위: `1..8`)
  - `1..7`: 해당 day 보상 claim 대기
  - `8`: 7일차까지 모두 claim 완료(다음 day reset 대기)

reset 조건(정본):
1. 출석 정보가 없을 때
2. `lastClaimUtcMs > 0`이고 `(serverNowUtcMs - lastClaimUtcMs) >= 72h`일 때
3. `nextAttendDay==8`이고, `lastClaimUtcMs`의 다음 UTC day로 넘어갔을 때

reset 동작:
- `nextAttendDay = 1`
- `claimedAttendUtcMs.Clear()`
- `lastClaimUtcMs = 0`
- `cycleStartUtcMs = toUtcDayStart(serverNowUtcMs)`
- reset 직후 runtime 상태는 `day1=CLAIMABLE`, `day2..7=WAIT`다.

접속 시점 상태 갱신:
- reset 판정 후 `lastLoginUtcMs = serverNowUtcMs`로 갱신한다.

데이터 누락 정책:
- `nextAttendDay`에 대응하는 `ATTEND.day` row가 없으면 보정을 수행하지 않는다.
- 해당 상태에서는 claim이 단순 실패/skip된다.

---

## D) Claim Rules (정본)

입력:
- `attend_id` (string)

출력:
- `GameResult<RewardData[]>`
- 성공 시 이번 claim으로 적용된 보상 목록을 반환한다.

claim 가능 조건:
1. row 존재
2. row가 운영 대상(activeRows + `day 1..7`)에 포함
3. `row.day == nextAttendDay`
4. 같은 UTC day에 이미 claim하지 않았음 (`toUtcDayStart(lastClaimUtcMs) != toUtcDayStart(serverNowUtcMs)`)
5. `claimedAttendUtcMs`에 해당 `attend_id`가 없음

claim 처리:
1. `RewardManager.ApplyRewardGroup(row.reward_group_id)` 호출
2. 실패면 storage mutation 없음
3. 성공이면 `AttendStorage.SetClaimed(row.attend_id, serverNowUtcMs)` 반영
4. day 진행:
   - `row.day < 7`이면 `nextAttendDay = row.day + 1`
   - `row.day == 7`이면 `nextAttendDay = 8` (완료 상태)
5. `SaveDataManager.SaveGameStorageAsync(true, ct)` 호출
6. 위 저장은 SaveData cloud payload 저장이며, Firestore 저장이 아니다.

claim 후 runtime 상태 전이:
- claim 당일에는 다음 day 슬롯이 즉시 열리지 않는다.
- 같은 UTC day 중복 claim 금지 규칙에 따라 다음 day는 `WAIT` 상태다.
- 다음 UTC day에 `RefreshCycle/Initialize`가 수행되면 현재 `nextAttendDay` 슬롯이 `CLAIMABLE`이 된다.

---

## E) Storage Schema (정본)

`AttendStorage` 기본 필드:

```csharp
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

규칙:
- key: `attend_id`
- value: 해당 attendId를 claim한 서버 UTC ms
- `nextAttendDay` 범위는 `1..8`
- `8`은 "7일차까지 완료, reset 대기" 상태다.

---

## F) SaveData JSON Section (정본)

루트 payload의 `attend` 섹션:

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

직렬화/역직렬화는 `SaveDataJsonCodecAttend`가 담당한다.

---

## G) Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Attend/AttendManager.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Attend/AttendStorage.cs`
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/SaveData/JsonCodec/SaveDataJsonCodecAttend.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/...`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/...`

---

## H) Attend Runtime Model (정본)

런타임 모델:

```csharp
public enum AttendRuntimeState
{
    NONE = 0,
    WAIT = 1,
    CLAIMABLE = 2,
    CLAIMED = 3,
}

public sealed class AttendRuntime
{
    public int Day { get; }
    public string Attend_id { get; }
    public string Reward_group_id { get; }
    public bool IsConfigured { get; }
    public AttendRuntimeState State { get; }
    public long ClaimedAtUtcMs { get; }
}
```

상태 규칙:
- `CLAIMED`: 해당 `attend_id`가 `claimedAttendUtcMs`에 존재
- `CLAIMABLE`: `day == nextAttendDay`이고, today 중복 claim이 아니며, row가 존재
- `WAIT`: 그 외 대기 상태(미래 day, 오늘 이미 claim, row 누락 포함)
- `NONE`: 런타임 미초기화/무효 입력 조회 등 예외적 조회 결과

---

## Related

- [10-attend-manager](../10-attend-manager/SKILL.md)
- [11-attend-storage](../11-attend-storage/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
