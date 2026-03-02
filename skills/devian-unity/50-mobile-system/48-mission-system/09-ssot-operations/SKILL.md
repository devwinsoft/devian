# 09-ssot-operations — 48-mission-system


Status: ACTIVE
AppliesTo: v10


이 문서는 Mission 시스템의 운영/테스트/DoD 정본이다.
테이블/규칙 정본은 [03-ssot](../03-ssot/SKILL.md)다.


---


## 운영 시나리오(정본)


### 1) 앱 시작 시

- 저장된 미션 상태(진행도/완료/클레임)를 로드한다.
- `MissionClockSnapshot`을 로드/갱신한다.
- `MissionManager.Storage.dailyMissionStartUtcMs`가 없으면 첫 sync의 `serverNowUtcMs`를 기록한다.
- 현재 sync의 `serverNowUtcMs`와 `dailyMissionStartUtcMs`의 차이가 7일 초과면 현재 `serverNowUtcMs`를 새 `dailyMissionStartUtcMs`로 사용하고 daily를 다시 시작한다.
- timed mission은 `dailyMissionStartUtcMs` anchor 기준으로 현재 `dailyKey`를 결정한다.
- daily period 전환 시 기존 daily runtime set을 정리하고, `MISSION_DAY` 전체 active row 중 최대 5개를 다시 선택해 runtime을 생성한다.
  - `fixed=true`는 항상 포함
  - 남은 슬롯은 `fixed=false` active row에서 random selection
- 현재 scope에서 이미 `ACTIVE`인 미션은 `missionUid`별 runtime을 보장한다. 이 시점이 mission start다.
- achievement는 `MISSION_ACHIEVE` 전체 active row를 검색하고, group별 저장 runtime이 있으면 restore 한다.
- achievement는 group별 저장 runtime이 없으면 `level=1` row로 최초 runtime을 생성한다.
- UI 상태(클레임 가능 여부)를 갱신한다.
- 첫 시작에서는 `isActive=true`인 미션을 활성화한다.


### 1-1) 오프라인/네트워크 장애 시

- 마지막으로 동기화한 `MissionClockSnapshot`이 있으면:
  - timed mission 화면 조회는 허용
  - period key 계산은 허용
  - claim도 클라이언트 추정 서버 시각 기준으로 수행할 수 있다
- 네트워크가 복구되면:
  - `BaseApplication.OnEnterForeground()` 같은 resume hook에서
  - `MissionManager.RefreshClockAsync()`를 호출해 서버 시간 기준을 다시 보정한다
- 첫 실행에서는 guest/google/apple login 이후에만 mission을 초기화한다.
- 현재 샘플 구조에서는 이 초기화 위치가 `TestSceneLoading.syncPurchaseStateAsync()` 이후 구간에 들어가는 것이 자연스럽다.
- 첫 login에서 `getMissionClock` 실패 시 MissionManager 초기화 실패로 보고 login 실패 처리한다.


### 2) 플레이 중(조건 평가)

- gameplay/system 레이어가 `MissionTriggerSystem.Notify(msgType, msgValue)`를 발행하면:
  - daily는 현재 cycle에서 선택된 row만 concrete runtime을 가진다.
  - achievement는 active group별 현재 runtime만 concrete runtime을 가진다.
  - runtime이 trigger를 받으면 자신의 `conditionOp` 규칙으로 `progressValue`를 갱신한다.
  - `conditionOp=MAX`면 max 갱신한다.
  - `conditionOp=SUM`이면 concrete runtime 구현이 누적 방식을 결정한다.
    - daily runtime: `conditionValue` 상한까지 누적
    - achieve runtime: 상한 없이 누적
  - 갱신 후 `progressValue >= conditionValue`이면 현재 `missionUid`를 `CLAIMABLE` 상태로 전환한다.
  - daily는 `CLAIMABLE` 시 구독을 해지한다.
  - achievement는 `CLAIMABLE`/`COMPLETED`여도 삭제 전까지 구독을 유지한다.


### 3) 클레임(보상 수령)

- MissionManager는 achievement면 현재 활성 runtime의 level을 내부에서 찾고, 이를 포함해 현재 period의 `grantId`를 생성한다(정본: [03-ssot](../03-ssot/SKILL.md)).
- MissionManager는 local claim record에서 `grantId` 존재 여부를 확인한다:
  - 이미 존재하면 즉시 실패/무시(중복 지급 방지)
- 미지급이면:
  1) timed mission이면 `dailyMissionStartUtcMs` anchor 기준 period key를 계산한다
  2) RewardManager로 "지급 실행(Apply)"을 위임한다
  3) local apply 성공 후 local claim record를 저장하고 현재 runtime을 `COMPLETED`로 전환한다
  4) achievement면 현재 활성 runtime 기준으로 다음 level row 존재 여부를 확인한다:
     다음 level row가 있으면 같은 runtime을 level up 한다
  5) `SaveDataManager`로 local save를 즉시 시도하고, 이어서 cloud save도 시도한다
  6) local save 실패 시 에러를 표시하고 플레이 불가능 상태로 전환한다(TODO)

권장 수렴 규칙:
- local save 성공 시 동일 `grantId`에 대한 중복 지급은 막을 수 있다.
- cloud save는 best effort이며, Firebase는 mission 정보를 저장하지 않는다.
- achievement는 수동 claim 모델이므로 `CLAIMABLE` runtime은 claim 전까지 유지한다.
- 마지막 level achievement는 `COMPLETED` 상태로 유지한다.


### 4) 기간 전환

- daily 전환 시 기존 daily runtime set을 정리하고 새 cycle용 runtime set을 다시 만든다.
- 새 cycle에서도 `MISSION_DAY` 전체 active row를 검색하지만 실제 생성은 최대 5개까지만 한다.
- `periodKey`는 현재 claim/reset 구간을 나타내는 메타데이터다.
- 이전 period의 지급 정보는 `grantId` 기준으로만 분리된다.


---


## 테스트 체크리스트(정본)

- daily 경계에서 중복 지급 0건(grantId 기준, local claim record 기준)
- 앱 재시작/크래시 후:
  - 완료/클레임 상태가 일관됨
  - local claim record 기준으로 중복 지급 0건 (Reward는 멱등 책임 없음)
- 서버 시간 재동기화 후:
  - timed mission period key가 다시 서버 기준으로 수렴한다
  - 현재 sync 시각과 `dailyMissionStartUtcMs` 차이가 7일 초과면 daily anchor reset이 정상 동작한다
- 메시지 누적 사용 시:
  - `MAX` row는 더 작은 값 trigger로 `progressValue`가 감소하지 않는다
  - `SUM` row는 trigger replay가 없을 때만 정확한 누적값을 유지한다
  - daily runtime의 `SUM`은 `conditionValue`를 넘지 않는다
  - achieve runtime의 `SUM`은 `conditionValue`를 넘어설 수 있다
  - daily는 runtime claimable/completed/dispose 시 구독 해지가 정상 동작한다
  - achievement는 삭제/파기 시 구독 해지가 정상 동작한다
- 컨텐츠 패치/테이블 교체로 `isActive` 값이 달라져도 UI/상태가 안전하게 동작


---


## DoD (구현 단계 기준)


Hard (반드시 0)
- 동일 기간(daily) 내 중복 지급 0건 (`grantId` 멱등)
- 기간 경계에서 상태 꼬임 0건(진행/완료/클레임)
- `MAX` / `SUM` 누적 규칙 오동작 0건
- 테이블 스키마와 실제 평가 로직 불일치 0건

Soft
- UI 그룹/정렬/표시 정책(필요 시)
