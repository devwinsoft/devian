# 12-leaderboard-platform-google — GPGS v2

Status: ACTIVE
AppliesTo: v10

## 범위

- Android(GPGS v2) Leaderboard 점수 보고

---

## 핵심 정책

- GPGS v2 전용 (`Google.Play.Games`)
- 플러그인 미설치 환경에서도 컴파일 가능해야 한다(Reflection)
- Android 런타임 외에서는 안전 실패
- 공개 API에서 Google SDK 타입/GPGS ID 비노출

---

## 기능

### Report Score

- 입력: `leaderboardId`, `score`
- 처리:
  - SSOT 매핑으로 `googleLeaderboardId` 변환
  - GPGS에 점수 보고
