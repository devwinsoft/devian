# 11-leaderboard-platform-apple — Game Center

Status: ACTIVE
AppliesTo: v10

## 범위

- iOS(Game Center) Leaderboard adapter
- 점수 보고
- local user snapshot 조회

---

## 핵심 정책

- iOS 런타임 외에서는 안전 실패
- 인증 실패 시 점수 보고 실패
- snapshot 조회에서 인증 실패는 `NotLoggedIn` 상태로 반환
- 공개 API에서 Apple SDK 타입/Game Center ID 비노출

---

## 기능

### Report Score

- 입력: `platformLeaderboardId`, `score`
- 처리:
  - (`LeaderboardManager`가 내부 `leaderboardId -> appleLeaderboardId` 매핑 후 호출)
  - Game Center에 점수 보고

### Load Player Snapshot

- 입력: `platformLeaderboardId`, `internalLeaderboardId`
- 처리:
  - `Social.CreateLeaderboard()` + `LoadScores`로 local user score 조회
  - 결과를 `LeaderboardPlayerSnapshotStatus`로 매핑
    - 성공 + 점수 있음: `Success`
    - 성공 + 점수 없음: `NoScore`
    - 인증 실패: `NotLoggedIn`
    - 예외/SDK 실패: `Failed`
