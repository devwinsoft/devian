---
name: mission-scheduler
description: Use this skill when defining or implementing MissionScheduler for runtime lifecycle and daily/period binding rebuild.
---

# 15-mission-scheduler

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Scheduler SSOT

## Purpose

`MissionScheduler`는 runtime lifetime 전담 collaborator다.

- create/restore/rebind/prune/lookup
- `missionUid` 발급
- table row resolve 후 bind 인자 구성

---

## Hard Rules

- MissionManager 내부 객체로만 사용한다.
- reward/claim/save orchestration은 하지 않는다.
- runtime 생성/복구는 `MissionRuntimeFactory`를 통해서만 수행한다.
- `condition_msg_id` resolve 실패 row는 runtime 생성 금지.

---

## Lifecycle Rules

### Daily (`MISSION_DAILY`)

- active row에서 최대 5개 선택(fixed 우선 + random)
- 선택 row만 create/restore
- `condition_msg_id -> GAME_MESSAGE` resolve 후 bind 인자 전달
- daily cycle 전환 시 기존 daily set 정리 후 재생성

### Weekly (`MISSION_WEEKLY`)

- 초기화/리셋 시 active row 전부 create/restore
- 기본 상태는 WAIT
- `MISSION_WEEKLY.day`를 activation group key로 사용한다.
- 활성화 규칙:
  - `day == 1`: 즉시 ACTIVE
  - `day == n`: `(n - 1)`일 경과 후 ACTIVE
- weekly cycle은 10일 단위
- cycle 전환 시 기존 weekly runtime 전량 정리 후 WAIT 재생성

---

## UID Rule

- `nextMissionUid` 기반 증가형 발급
- 충돌 UID는 건너뛴다

---

## Notes

- legacy progress seed 브릿지는 사용하지 않는다.
- progress 정본은 runtime local state다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
