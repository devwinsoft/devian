# 09-ssot-operations — 27-attend-system

Status: ACTIVE
AppliesTo: v10

이 문서는 출석 시스템 운영/테스트/DoD 정본이다.
핵심 규칙 정본은 [03-ssot](../03-ssot/SKILL.md)다.

---

## 운영 시나리오

### 1) 앱 시작

- SaveData load 후 `AttendManager.InitializeAsync(ct)`를 호출한다.
  - 출석 정보 없음
  - 마지막 출석 수령 후 72시간 경과
  - 7일차 claim 다음 UTC day
- 초기화 직후 day `1..7` runtime을 항상 생성한다.
- reset 케이스면 상태는 `day1=CLAIMABLE`, `day2..7=WAIT`다.

### 2) 출석 목록 표시

- `AttendManager.Runtimes`(7개 고정 슬롯)를 UI 데이터 소스로 사용한다.
- 각 슬롯 상태(`CLAIMABLE / CLAIMED / WAIT`)를 그대로 표시한다.
- day row 누락 슬롯(`isConfigured=false`)은 보정 없이 `WAIT` 상태로 표시한다.

### 3) 출석 claim

- UI가 `AttendManager.ClaimAsync(attendId, ct)`를 호출한다.
- 성공 시:
  - 반환값 `RewardData[]`로 지급 결과를 받는다.
  - `RewardManager`로 보상 적용
  - storage 반영
  - SaveData 저장
- 실패 시:
  - storage 불변
  - UI는 에러 처리

### 4) cycle rollover

- `nextAttendDay == 8`이고 다음 UTC day가 되면 cycle reset한다.
- `lastClaimUtcMs` 기준 72시간 경과 후 재접속이면 reset한다.

---

## 테스트 체크리스트

- `isActive=false` row는 항상 claim 대상에서 제외된다.
- `day<=0`, `day>7`, `rewardGroupId` empty row는 제외된다.
- 동일 `attendId` 재claim은 실패한다.
- 같은 UTC day 중복 claim은 실패한다.
- `row.day != nextAttendDay` claim은 실패한다.
- `nextAttendDay` row가 테이블에 없으면 보정 없이 claim 실패/skip된다.
- `RewardManager.ApplyRewardGroup` 실패 시 claim 상태가 기록되지 않는다.
- 72시간 경과 reset 시 claim map이 초기화된다.
- 7일차 claim 다음 날 reset 시 claim map이 초기화된다.
- 초기화 직후 runtime 개수는 항상 7개다.
- reset 직후 runtime 상태가 `1일 claimable + 2~7일 wait`인지 확인한다.
- claim 성공 당일에는 다음 day runtime이 `WAIT` 유지인지 확인한다.
- 다음 UTC day 진입 후 refresh 시 `nextAttendDay` runtime이 `CLAIMABLE` 전이되는지 확인한다.
- Save/Load 이후 claim 상태가 유지된다.

---

## DoD

Hard (반드시 0)
- `27-attend-system` 문서 세트(00/01/03/09/10/11)가 존재한다.
- `ATTEND` 기반 해석 규칙이 03-ssot에 명시된다.
- `AttendManager`가 `RewardManager` 경유로만 보상을 적용한다.
- `AttendStorage`가 SaveData codec 경로에 연결된다.
- Login 초기화 경로에서 `AttendManager.InitializeAsync`가 호출된다.
