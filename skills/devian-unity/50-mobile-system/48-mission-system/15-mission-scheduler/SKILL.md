---
name: mission-scheduler
description: Use this skill when defining or implementing MissionScheduler for runtime lifecycle, missionStat resolution, and daily/achieve binding rebuild.
---

# 15-mission-scheduler

Status: ACTIVE  
AppliesTo: v10  
Type: Design / Scheduler SSOT

## Purpose

`MissionScheduler`는 runtime lifetime 전담 collaborator다.

- create/restore/rebind/prune/lookup
- `missionUid` 발급
- `missionStatId -> MISSION_STAT` resolve 후 bind 인자 구성

---

## Hard Rules

- MissionManager 내부 객체로만 사용한다.
- reward/claim/save orchestration은 하지 않는다.
- runtime 생성/복구는 `MissionRuntimeFactory`를 통해서만 수행한다.
- `MISSION_STAT` resolve 실패 row는 runtime 생성 금지.

---

## Lifecycle Rules

### Daily

- active row에서 최대 5개 선택(fixed 우선 + random)
- 선택 row만 create/restore
- `missionStatId` resolve 후 `StatType/OpType` 전달
- period 전환 시 기존 daily set 정리 후 재생성

### Achieve

- group별 runtime 1개 보장
- create/restore 시 `missionStatId` resolve
- external progress reader(`stats[missionStatId]`)를 factory args로 전달
- next level create는 scheduler가 하지 않는다(Claim 흐름의 runtime mutation)

---

## UID Rule

- `nextMissionUid` 기반 증가형 발급
- 충돌 UID는 건너뛴다

---

## Notes

- legacy progress seed 브릿지는 사용하지 않는다.
- achieve progress 정본은 storage stats다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-mission-manager](../10-mission-manager/SKILL.md)
- [14-mission-factory](../14-mission-factory/SKILL.md)
