# 09-ssot-operations — 48-mission-system


Status: ACTIVE
AppliesTo: v10


이 문서는 Mission 시스템의 운영/테스트/DoD 정본이다.
테이블/규칙 정본은 [03-ssot](../03-ssot/SKILL.md)다.


---


## 운영 시나리오(정본)


### 1) 앱 시작 시

- 저장된 미션 상태(진행도/완료/클레임)를 로드한다.
- 서버 시간 기준으로 daily/weekly 경계를 계산하고, resetRule에 따라 리셋한다.
- NEEDS CHECK: 서버 시간 소스/갱신 주기/오프라인 정책은 구현 단계에서 확정한다.
- 리셋 후 UI 상태(클레임 가능 여부)를 갱신한다.


### 2) 플레이 중(조건 평가)

- 게임 이벤트/스탯 변경 입력을 받으면:
  - `MISSION` 테이블의 조건을 평가하여 진행도를 갱신한다.
  - 완료 조건을 만족하면 "완료"로 전환한다(클레임 가능 상태).


### 3) 클레임(보상 수령)

- MissionManager는 category에 따라 `grantId`를 생성한다(정본: [03-ssot](../03-ssot/SKILL.md)).
- MissionManager는 로컬 ledger에서 `grantId` 상태를 확인한다:
  - 이미 `granted`면 즉시 실패/무시(중복 지급 방지)
- 미지급이면:
  1) ledger를 `pending`으로 기록
  2) RewardManager로 "지급 실행(Apply)"을 위임
  3) 성공 시 ledger를 `granted`로 확정 + claimed 상태 저장
  4) 실패 시 ledger `pending` 유지(앱 시작 시/수동 재시도에서 다시 Apply)


---


## 테스트 체크리스트(정본)

- daily/weekly 리셋 경계에서 중복 지급 0건(grantId 기준, Mission ledger 기준)
- 앱 재시작/크래시 후:
  - 완료/클레임 상태가 일관됨
  - 클레임 재시도 시 Mission ledger 기준으로 중복 지급 0건 (Reward는 멱등 책임 없음)
- `MISSION` 테이블 변경(isActive 토글) 시 UI/상태가 안전하게 동작


---


## DoD (구현 단계 기준)


Hard (반드시 0)
- 동일 기간(daily/weekly) 내 중복 지급 0건 (`grantId` 멱등)
- 리셋 경계에서 상태 꼬임 0건(진행/완료/클레임)
- 테이블 스키마와 실제 평가 로직 불일치 0건

Soft
- UI 그룹/정렬/표시 정책(필요 시)
