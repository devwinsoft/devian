# 12-leaderboard-platform-google — GPGS v2

Status: ACTIVE
AppliesTo: v10

## 범위

- Android(GPGS v2) Leaderboard adapter
- 점수 보고
- local user snapshot 조회

---

## 핵심 정책

- GPGS v2 전용 (`Google.Play.Games`)
- 플러그인 미설치 환경에서도 컴파일 가능해야 한다(Reflection)
- Android 런타임 외에서는 안전 실패
- 인증 미완료 시 snapshot은 `NotLoggedIn` 상태로 반환
- 공개 API에서 Google SDK 타입/GPGS ID 비노출

---

## 기능

### Report Score

- 입력: `platformLeaderboardId`, `score`
- 처리:
  - (`LeaderboardManager`가 내부 `leaderboardId -> googleLeaderboardId` 매핑 후 호출)
  - GPGS에 점수 보고

### Load Player Snapshot

- 입력: `platformLeaderboardId`, `internalLeaderboardId`
- 처리:
  - reflection으로 GPGS API 호출하여 local user score 조회
  - 결과를 `LeaderboardPlayerSnapshotStatus`로 매핑
    - 성공 + 점수 있음: `Success`
    - 성공 + 점수 없음: `NoScore`
    - 인증 실패: `NotLoggedIn`
    - API 실패: `Failed`
    - 플랫폼 미지원: `PlatformUnavailable`
