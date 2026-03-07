# 50-leaderboard — Policy

Status: ACTIVE
AppliesTo: v10
Type: Policy / Entry Point

## Purpose

Leaderboard 점수 시스템의 모듈 경계와 하드룰을 정의한다.

---

## Hard Rules

### 1) 상위 로직에는 내부 ID만 노출한다

- 외부 API는 `leaderboardId`(내부 ID)만 사용한다.
- 플랫폼 문자열 ID는 SSOT 매핑 레이어에만 존재한다.

### 2) LeaderboardManager는 점수 제출만 책임진다

- `ReportScoreAsync`만 책임진다.
- 업적 Unlock/Sync는 `AchieveManager` 책임이다.

연관: [46-achieve-system/01-policy](../../46-achieve-system/01-policy/SKILL.md)

### 3) Initialize는 명시적 호출이며 자동 초기화 금지

- `InitializeAsync(ct)`는 명시적으로 호출한다.
- 초기화 전 API 호출은 실패 반환.

### 4) 미지원 플랫폼/에디터는 안전 실패

- 예외 폭발 없이 `CommonResult` 실패로 종료한다.

### 5) 공개 경계에서 플랫폼 의존 타입/필드 비노출

- 공개 API/DTO에 `apple*Id`, `google*Id`를 노출하지 않는다.
- 공개 API 시그니처에 플랫폼 SDK 타입을 노출하지 않는다.

---

## Client API

- `InitializeAsync(ct)` -> `Task<CommonResult>`
- `ReportScoreAsync(leaderboardId, score, ct)` -> `Task<CommonResult>`
