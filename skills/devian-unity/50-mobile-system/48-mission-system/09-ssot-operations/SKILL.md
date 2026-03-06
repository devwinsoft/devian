# 09-ssot-operations — 48-mission-system

Status: ACTIVE  
AppliesTo: v10

운영 시나리오/테스트/DoD 정본이다.  
테이블/구조 정본은 [03-ssot](../03-ssot/SKILL.md)를 따른다.

---

## 운영 시나리오

### 1) 앱 시작

- MissionStorage 로드
- clock snapshot 동기화
- daily anchor(`dailyMissionStartUtcMs`) 초기화/보정
- scheduler가 runtime 재구성
  - daily: active row 중 최대 5개 선택
  - achieve: group별 runtime 1개 create/restore
- achieve runtime은 `stats[missionStatId]` reader와 함께 복구된다.

### 2) 플레이 중 trigger 입력

- 이벤트 발행: `MissionManager.Notify(statType, delta)`
- 처리 순서:
  1. MissionManager가 `stats[missionStatId]` 갱신
  2. 내부 TriggerSystem runtime notify
- daily runtime:
  - `MAX`: max 갱신
  - `SUM`: conditionValue 상한으로 누적
- achieve runtime:
  - 내부 누적하지 않고 `stats[missionStatId]`를 읽어 반영

### 3) claim

- claimable 검증 후 RewardManager apply
- `ACHIEVE`에서 다음 level row가 있으면 같은 runtime level-up
  - 기존 구독 해지
  - 새 `missionStatId/statType/opType` 바인딩
  - 새 statType 재구독
- save 즉시 실행

### 4) period 전환

- daily cycle 전환 시 기존 daily runtime set 정리 후 재선택
- achieve는 period reset 없음(`once`)

---

## 테스트 체크리스트

- trigger 1회당 `stats` 갱신이 1회만 일어나는지
- `MISSION_STAT_TYPE` 변경 시 정상 라우팅되는지
- daily `SUM`이 `conditionValue`를 넘지 않는지
- achieve progress가 `stats[missionStatId]`와 일치하는지
- achieve level-up 시 기존 구독 해지 + 새 구독 등록이 정확한지
- achieve 저장 payload에 `periodKey`/`progressValue`가 없는지
- legacy fallback 없이 schema v2만으로 동작하는지

---

## DoD

Hard:
- `missionStatId` 미해결 row에서 런타임 생성 0건
- level-up 후 구독 누락/중복 0건
- trigger-처리 순서 역전(`notify 먼저`) 0건
- save/restore 후 `stats`와 runtime state 불일치 0건

Soft:
- UI 재바인딩 시 메시지 순서 일관성 유지
