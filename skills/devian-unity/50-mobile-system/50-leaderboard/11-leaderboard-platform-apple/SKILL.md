# 11-leaderboard-platform-apple — Game Center

Status: ACTIVE
AppliesTo: v10

## 범위

- iOS(Game Center) Leaderboard 점수 보고

---

## 핵심 정책

- iOS 런타임 외에서는 안전 실패
- 인증 실패 시 점수 보고 실패
- 공개 API에서 Apple SDK 타입/Game Center ID 비노출

---

## 기능

### Report Score

- 입력: `leaderboardId`, `score`
- 처리:
  - SSOT 매핑으로 `appleLeaderboardId` 변환
  - Game Center에 점수 보고
