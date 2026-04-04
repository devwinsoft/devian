# 27-attend-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

출석 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) ATTEND 테이블이 단일 정본이다

- 입력 SSOT: `input/Domains/Game/MetaTable.xlsx` 의 `ATTEND` 시트
- 런타임 조회는 `TB_ATTEND`(Generated)만 사용한다.
- 수동 하드코딩 목록(배열/상수)으로 출석 row를 재정의하지 않는다.

### 2) 서버 시각 기준으로 day를 계산한다

- `DateTime.Now`/디바이스 로컬 타임존 기반 계산을 금지한다.

### 3) 보상 적용은 RewardManager 단일 경로로 위임한다

- claim 성공 시 `RewardManager.ApplyRewardGroup(reward_group_id)`를 사용한다.
- AttendManager가 Inventory를 직접 수정하지 않는다.
- Attend claim 결과를 Firebase Firestore(Functions 포함)로 직접 저장하지 않는다.

### 4) claim 상태 정본은 AttendStorage다

- `AttendManager`가 `AttendStorage`를 소유한다.
- claim 처리 성공 후 `AttendStorage`를 갱신한다.
- 저장은 `SaveDataManager.SaveGameStorageAsync(true, ct)` 경로를 사용한다.

### 5) ATTEND active row만 운영 대상이다

- `Is_active == true` row만 claim 후보가 된다.
- `day < 1` 또는 `day > 7` row는 운영 대상에서 제외한다.
- `reward_group_id` empty row는 invalid row로 간주하고 제외한다.

### 6) reset 조건은 3개로 고정한다

- (1) 출석 정보가 없을 때
- (2) 마지막 출석 수령 시각(`lastClaimUtcMs`) 기준 72시간 이상 경과 후 재접속했을 때
- (3) 7일차 보상 수령 후 다음 UTC day로 넘어갔을 때
- reset 시 `nextAttendDay=1`, claim map clear를 수행한다.

### 9) attend runtime은 day 1..7 고정 7개를 생성한다

- 초기화/리프레시 시 런타임 슬롯을 항상 7개 생성한다.
- reset 직후 상태는 `day1=CLAIMABLE`, `day2..7=WAIT`다.
- day row 누락 슬롯은 보정 없이 `WAIT` 상태를 유지한다.

### 8) 데이터 누락 시 보정 로직을 두지 않는다

- ATTEND 테이블 day 누락/비연속 상황에서 자동 보정(다음 day 재매핑/대체 보상)을 하지 않는다.
- 현재 `nextAttendDay`에 대응하는 row가 없으면 claim은 단순 실패/skip 처리한다.

### 7) SaveData codec 외 직렬화 경로 금지

- `AttendStorage`에 `ToJson()/FromJson()`를 추가하지 않는다.
- 직렬화/역직렬화는 `SaveDataJsonCodecAttend`로만 처리한다.
- 저장 경로는 SaveData local/cloud payload이며, Firestore 문서 저장이 아니다.

---

## Error Codes

- claim 실패 에러코드는 `GAME_ERROR_TYPE.ATTEND_*`를 사용한다.
  - 예: `GAME_ERROR_TYPE.ATTEND_SYNC_REQUIRED`, `GAME_ERROR_TYPE.ATTEND_INVALID_ROW`
