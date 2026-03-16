# 09-ssot-operations — 48-mission-system

Status: ACTIVE  
AppliesTo: v10

운영 시나리오/테스트/DoD 정본이다.  
테이블/구조 정본은 [03-ssot](../03-ssot/SKILL.md)를 따른다.

---

## 운영 시나리오

### 1) 앱 시작

- MissionStorage 로드
- 서버 시각 동기화
- daily/period anchor(`dailyMissionStartUtcMs`, `periodMissionStartUtcMs`) 초기화/보정
- scheduler가 runtime 재구성
  - daily: active row 중 최대 5개 선택
  - period: active row 전부 생성(WAIT), `day` 규칙으로 즉시/지연 활성화

### 2) 플레이 중 trigger 입력

- 이벤트 발행: `GameMessageManager.Notify(messageType, delta)`
- 처리 순서:
  1. `message.stats[messageId]` 갱신
  2. `GameMessageTrigger` publish
  3. runtime 반영 (`DAILY`/`PERIOD`)
- WAIT runtime은 progress 이벤트를 소비하지 않는다.

### 3) claim

- claimable 검증 후 RewardManager apply
- runtime 상태 갱신
- save 즉시 실행

### 4) 주기 전환

- daily cycle 전환 시 기존 daily runtime set 정리 후 재선택
- period cycle(10일) 전환 시 period runtime 전량 WAIT 재생성 후 day 규칙 재활성화

---

## 테스트 체크리스트

- trigger 1회당 `message.stats` 갱신이 1회만 일어나는지
- `GAME_MESSAGE_TYPE` 변경 시 정상 라우팅되는지
- daily/period `SUM` 누적이 음수로 내려가지 않는지
- period `day=1` 즉시 활성화가 보장되는지
- period `day=n`이 `(n-1)`일 후에만 활성화되는지
- period 10일 리셋 타이밍에서 중복 runtime 생성이 없는지

---

## DoD

Hard:
- `conditionMsgId` 미해결 row에서 runtime 생성 0건
- level-up 후 구독 누락/중복 0건
- WAIT runtime에서 progress 콜백 전송 0건
- period cycle reset 후 stale runtime 잔존 0건

Soft:
- UI 재바인딩 시 메시지 순서 일관성 유지
