# 09-ssot-operations — 50-leaderboard

Status: ACTIVE
AppliesTo: v10

이 문서는 Leaderboard 점수 시스템 운영/테스트/DoD 정본이다.

---

## 운영 시나리오

### 1) 앱 시작/로그인

- `InitializeAsync(ct)`

### 2) 점수 갱신 시점

- 상위 로직이 점수를 계산한 뒤 `ReportScoreAsync(leaderboardId, score, ct)` 호출
- 음수 점수는 즉시 실패 반환

---

## 테스트 체크리스트

- Editor/미지원 플랫폼 안전 실패
- iOS(Game Center) 점수 제출 성공/실패 케이스
- Android(GPGS v2) 점수 제출 성공/실패 케이스
- 공개 API에서 플랫폼 의존 타입/필드 비노출

---

## DoD

- 초기화 전 API 예외 0건
- 미지원 플랫폼 크래시 0건
- 공개 경계 플랫폼 의존성 노출 0건
