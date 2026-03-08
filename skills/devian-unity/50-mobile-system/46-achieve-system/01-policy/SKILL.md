# 46-achieve-system — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

업적 시스템(`AchieveManager`)의 공개 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) runtime 정본은 `ACHIEVE` table + `AchieveStorage`다

- 업적 row는 `ACHIEVE`를 사용한다.
- 런타임 저장 정본은 `AchieveStorage.runtimes`다.
- progress는 `MESSAGE.saveType`에 따라 결정된다.
  - `TOTAL_*`: `GameMessageStorage` 값을 projection한다.
  - `SESSION_*`: `AchieveRuntime.progressValue`를 직접 유지한다.
- 초기화 자동 생성 대상은 `ACHIEVE.achieveType == ACHIEVE_TYPE.DEFAULT`인 level=1 row다.

### 2) 외부에는 내부 업적 ID만 노출한다

- 공개 API는 `achievementId`(= `ACHIEVE.achieveId`)만 받는다.
- `appleAchievementId`, `googleAchievementId`는 매핑 레이어 내부에만 존재한다.

### 3) trigger 입력은 `GameMessageManager`를 통해 받는다

- `GameMessageManager.Notify(GAME_MESSAGE_TYPE, delta)`가 유일한 외부 입력이다.
- `AchieveManager`는 `GameMessageManager` 트리거를 구독해 해당 runtime projection을 동기화한다.

### 4) 업적 알림은 `ACHIEVE_MESSAGE`를 사용한다

- `AchieveMessageTrigger`의 키 타입은 `ACHIEVE_MESSAGE`다.
- 외부 구독은 `AchieveManager.Subcribe/SubcribeOnce/UnSubcribe` 헬퍼만 사용한다.
- runtime 입력(`GAME_MESSAGE_TYPE`)과 알림(`ACHIEVE_MESSAGE`)은 서로 다른 책임으로 혼용하지 않는다.

### 5) claim은 AchieveManager가 orchestration 한다

- claim 가능 상태 확인
- reward apply 위임(RewardManager)
- next level 있으면 재바인딩
- 없으면 completed
- 저장(SaveData) 수행
- 플랫폼 업적 unlock은 best-effort로 처리한다.

### 6) level-up은 반드시 바인딩 전환을 수행한다

- 다음 level row 기준으로 `messageId/messageType/saveType/conditionValue`를 교체한다.
- 다음 `messageType` 기준 projection 동기화로 전환한다.

### 7) period 개념은 없다

- Achieve runtime에는 `periodKey`가 없다.
- scheduler도 사용하지 않는다.

### 8) Initialize는 명시적 호출이다

- `InitializeAsync(ct)`는 명시적으로 호출한다.
- 초기화 전 claim/unlock/sync API 호출은 실패를 반환한다.

---

## Related

- [03-ssot](../03-ssot/SKILL.md)
- [10-achieve-manager](../10-achieve-manager/SKILL.md)
- [13-achieve-runtime](../13-achieve-runtime/SKILL.md)
- [14-achieve-storage](../14-achieve-storage/SKILL.md)
- [15-achieve-message-trigger](../15-achieve-message-trigger/SKILL.md)
