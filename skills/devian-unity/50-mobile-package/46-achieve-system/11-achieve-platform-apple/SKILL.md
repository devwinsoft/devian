# 11-achieve-platform-apple — Game Center

Status: ACTIVE
AppliesTo: v10

## 범위

- iOS(Game Center) 업적 완료 보고
- iOS(Game Center) 업적 상태 Sync

---

## 정책

- iOS 런타임 외에는 안전 실패
- 인증되지 않은 사용자 상태에서는 Unlock/Sync 실패
- 상위 API에는 Apple SDK 타입/ID를 노출하지 않는다.

---

## 동작

### Unlock

- 내부 `achievementId`를 SSOT 매핑으로 `appleAchievementId`로 변환
- `ReportProgress(..., 100%)`로 완료 보고

### Sync

- `LoadAchievements`로 상태 조회
- `completed || percent>=100` 판정값을 Manager로 전달
