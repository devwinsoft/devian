# 03-ssot — 48-mission-system (통합 SSOT)


Status: ACTIVE
AppliesTo: v10


## 이 문서가 정본이다 (SSOT)

- Game 도메인 테이블 파일: `input/Domains/Game/MissionTable.xlsx`
- Game 도메인 테이블 파일: `input/Domains/Game/RewardTable.xlsx`
- `MISSION_DAILY` 테이블 스키마 (일일 미션)
- `MISSION_WEEKLY` 테이블 스키마 (주간 미션)
- `MISSION_ACHIEVEMENT` 테이블 스키마 (업적 미션)
- grantId 생성 규칙(반복 지급 포함)
- daily/weekly 기간 키(dailyKey/weeklyKey) 계산 규칙(서버 시간 기준)


---


## A) Core Terms (정본)

- `missionId`: 내부 표준 미션 ID(string)
- `conditionType`: 조건 종류(예: `StatAtLeast`, `CountAtLeast`)
- `conditionKey`: 조건 입력 키(예: `stage_clear_count`)
- `conditionOp`: 연산자 표현(예: `>=`)
- `conditionValue`: 목표값(int)
- `rewardKey`: 보상 키(string). Game REWARD 테이블의 `rewardId`(pk)를 참조한다.

NOTE:
- 일일/주간/업적은 **테이블을 분리**한다:
  - `MISSION_DAILY`
  - `MISSION_WEEKLY`
  - `MISSION_ACHIEVEMENT`
- `uiGroup` 컬럼은 사용하지 않는다(이번 SSOT의 정본 스키마에 포함하지 않음).


---


## B) Table Schema (설계 정본)

세 테이블은 동일한 스키마를 사용한다.

| field | type | note |
|------|------|------|
| `missionId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `conditionType` | string | 조건 종류 |
| `conditionKey` | string | 조건 입력 키 |
| `conditionOp` | string | `>=` 등(표현만) |
| `conditionValue` | int | 목표값 |
| `rewardKey` | string | 보상 키(= Game REWARD.rewardId(pk)) |

NOTE:
- condition 표현은 "테이블로 표현 가능한 최소 형태"만 정한다.
- 복잡한 조건(AND/OR 등)은 필요해질 때 확장한다(이번 작업 범위 밖).


---


## C) grantId 규칙(정본)

MissionManager는 아래 규칙으로 `grantId`를 생성한다.

- dailyKey / weeklyKey는 **서버 시간 기준**으로 계산된 "기간 키"다.
- dailyKey / weeklyKey의 구체 리셋 경계(시각/타임존/주간 시작 요일)는 제품 정책이며, 구현 단계에서 확정한다(NEEDS CHECK).

- daily (`MISSION_DAILY`):
  - `grantId = "mission:{missionId}:{dailyKey}"`
  - dailyKey format: `yyyy-mm-dd` (서버 시간 기준으로 계산된 날짜 키)
- weekly (`MISSION_WEEKLY`):
  - `grantId = "mission:{missionId}:{weeklyKey}"`
  - weeklyKey format: `yyyy-mm-dd` (서버 시간 기준으로 계산된 주간 시작일 키)
- achievement (`MISSION_ACHIEVEMENT`, 1회성):
  - `grantId = "mission:{missionId}:once"`

- `grantId`는 MissionManager의 **개별 지급 기록 키(로컬 ledger key)** 다.
- RewardManager는 `grantId`를 사용하지 않는다(멱등/기록 책임 없음).

연관: [49-reward-system/03-ssot](../../49-reward-system/03-ssot/SKILL.md)


---


## D) Mission Claim Ledger (정본)

MissionManager는 `grantId` 단위로 아래 상태를 로컬에 저장한다.

- `none`: 기록 없음(미지급)
- `pending`: 지급 결정을 했지만 Reward 적용이 아직 확정되지 않음
- `granted`: Reward 적용 완료(중복 지급 방지 기준)

NEEDS CHECK(구현 단계):
- ledger의 구체 구조/저장 위치(SaveData 스키마)
