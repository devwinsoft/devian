# 12-achieve-platform-google — GPGS v2

Status: ACTIVE
AppliesTo: v10

## 범위

- Android(GPGS v2) 업적 완료 보고
- Android(GPGS v2) 업적 상태 Sync

---

## 정책

- GPGS v2 전용 (`Google.Play.Games`)
- 플러그인 미설치 환경 컴파일 안전성 확보(Reflection)
- Android 런타임 외에는 안전 실패
- 인증되지 않은 사용자 상태에서는 Unlock/Sync 실패

---

## 동작

### Unlock

- 내부 `achievementId`를 SSOT 매핑으로 `googleAchievementId`로 변환
- `ReportProgress(..., 100%)` 완료 보고

### Sync

- Reflection으로 `LoadAchievements(Action<IAchievement[]>)` 호출
- `completed || percent>=100` 판정값을 Manager로 전달
