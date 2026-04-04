# 10-attend-manager

Status: ACTIVE
AppliesTo: v10

`AttendManager` 설계 문서다.
출석 시스템의 오케스트레이터로서 ATTEND row 해석, claim 판정, 보상 적용, 저장까지 담당한다.

---

## Class Design

```csharp
public sealed class AttendManager : CompoSingleton<AttendManager>
{
    readonly AttendStorage _storage = new();
    public AttendStorage Storage => _storage;
    public bool IsInitialized { get; }
    public IReadOnlyList<ATTEND> ActiveRows { get; }
    public IReadOnlyList<AttendRuntime> Runtimes { get; }

    public Task<CommonResult> InitializeAsync(CancellationToken ct = default);
    public void RefreshCycle();
    public int GetCurrentCycleDay();
    public AttendRuntimeState GetRuntimeState(string attend_id);
    public AttendRuntime GetRuntime(int day);
    public bool IsClaimed(string attend_id);
    public bool IsClaimable(string attend_id);
    public Task<GameResult<RewardData[]>> ClaimAsync(string attend_id, CancellationToken ct = default);
    public void ClearStorage();
}
```

---

## Responsibilities

- `TB_ATTEND` active row를 로드/정렬한다.
- day 1~7 출석 row만 운영 대상으로 사용한다.
- 초기화/리프레시 시 day 1~7 고정 슬롯의 `AttendRuntime` 7개를 생성한다.
- reset 직후 runtime 상태를 `day1=CLAIMABLE`, `day2..7=WAIT`로 구성한다.
- reset 조건 3개(정보 없음/72시간 경과/7일차 다음 날)를 판정한다.
- `nextAttendDay` row 누락 시 별도 보정 없이 claim 불가로 처리한다.
- claim 가능 여부를 판정한다.
- claim 성공 시 `RewardManager.ApplyRewardGroup`으로 지급을 실행한다.
- claim 성공 결과로 적용된 `RewardData[]`를 반환한다.
- `AttendStorage`를 갱신하고 SaveData 저장을 호출한다.

비책임:
- 서버 ledger/멱등 보장
- Inventory 직접 수정
- ATTEND 테이블 생성/로드 파이프라인

---

## Dependencies

- `RewardManager` — 보상 적용
- `SaveDataManager` — 영속화
- `Devian.Domain.Game` — `TB_ATTEND`, `ATTEND`

---

## Failure Rules

- 초기화 전 claim 호출은 실패(`SAVEDATA_SYNC_REQUIRED`)다.
- row 미존재/invalid/순서 불일치(`row.day != nextAttendDay`)는 실패다.
- `nextAttendDay`에 해당하는 row 누락은 실패/skip 처리한다.
- 같은 UTC day 중복 claim은 실패다.
- 7일차 완료 후(reset 전) claim 호출은 실패다.
- 보상 적용 실패 시 저장은 수행하지 않는다.
- claim 당일에는 다음 day runtime이 `CLAIMABLE`로 전이되지 않고 `WAIT`를 유지한다.

---

## Implementation Location (3-path mirror)

- UPM (정본):
  - `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Runtime/Attend/AttendManager.cs`
- Packages (sync):
  - `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Runtime/Attend/AttendManager.cs`
- Assets/Samples (import):
  - `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobilePackage/Runtime/Attend/AttendManager.cs`

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [11-attend-storage](../11-attend-storage/SKILL.md)
- [49-reward-system/10-reward-manager](../../49-reward-system/10-reward-manager/SKILL.md)
- [21-savedata-system/43-savedata-json-codec](../../21-savedata-system/43-savedata-json-codec/SKILL.md)
