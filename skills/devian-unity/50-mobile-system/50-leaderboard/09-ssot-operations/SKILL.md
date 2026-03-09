# 09-ssot-operations — 50-leaderboard

Status: ACTIVE
AppliesTo: v10

이 문서는 Leaderboard 점수 + 시즌 보상 운영/테스트/DoD 정본이다.

---

## 운영 시나리오

### 1) 앱 시작/로그인

- `LeaderboardManager.InitializeAsync(ct)`

### 2) 점수 갱신 시점

- 상위 로직은 점수를 직접 넘기지 않는다.
- `LeaderboardManager.ReportScoreAsync(leaderboardId, ct)` 호출
- score는 `LEADERBOARD.messageId` 기준으로 내부 조회한다.
- 시즌 활성 기간 외에는 점수 기록 실패 반환 (`LEADERBOARD.seasonId → TB_SEASON` 시간 확인)

### 3) 시즌 전환 보상 평가 시점

- foreground 전이/앱 시작 시 `LeaderboardManager.SyncSeasonTransitionRewardsAsync(ct)` 호출
- 서버 시간(`MissionManager.TryGetServerNowUtcMs`)이 없으면 스킵
- 평가 대상: 현재 시즌의 직전 시즌(모드별)
- grace period 통과 후(`+10분`)에만 평가

### 4) 시즌 보상 평가 결과 처리

- `Success + HasScore=true`:
  - rank로 `LEADERBOARD_REWARD` 구간 매칭
  - 매칭 성공: `RewardManager.ApplyRewardGroup(rewardGroupId)` + `CLAIMED` 기록
  - 매칭 실패: `RANK_OUT_OF_REWARD` 기록
- `NoScore`: `NO_PARTICIPATION` 기록
- `PlatformUnavailable` / `NotLoggedIn` / `Failed`: 기록하지 않고 재시도

---

## 테스트 체크리스트

- Editor/미지원 플랫폼 안전 실패
- iOS(Game Center) 점수 제출 성공/실패 케이스
- Android(GPGS v2) 점수 제출 성공/실패 케이스
- snapshot 상태별 처리(`Success`/`NoScore`/`PlatformUnavailable`/`NotLoggedIn`/`Failed`)
- grace period 경계:
  - 시즌 종료 + 9분: 미평가
  - 시즌 종료 + 10분: 평가 시작
- `processedClaims` 중복 지급 방지(같은 `leaderboardId` 1회)
- `LEADERBOARD_REWARD` 구간 매칭/미매칭 분기
- 시즌 외 기간 점수 기록 → 실패 반환
- 공개 API에서 플랫폼 의존 타입/필드 비노출

---

## DoD

- 초기화 전 API 예외 0건
- 미지원 플랫폼 크래시 0건
- grace period 하드코딩 0건 (상수만 사용)
- 시즌 보상 dedupe 누락 0건 (`processedClaims` 단일 기준)
- 하드코딩 보상 지급 0건 (`rewardGroupId` 경유만 허용)
- 시즌 외 점수 기록 차단 정상 동작
- 공개 경계 플랫폼 의존성 노출 0건
