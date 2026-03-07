# 03-ssot — 50-leaderboard

Status: ACTIVE
AppliesTo: v10

## SSOT 범위

이 문서는 Leaderboard 점수 시스템의 정본이다.

- 내부 리더보드 ID
- 플랫폼 리더보드 ID 매핑

업적 SSOT는 `46-achieve-system`이 정본이다.

---

## A. Internal ID

- `leaderboardId`: 내부 표준 리더보드 ID (string)

---

## B. Mapping Source

- 파일: `input/Domains/Game/GameTable.xlsx`
- 시트: `LEADERBOARD`

---

## C. Table Schema

| field | type | note |
|------|------|------|
| `leaderboardId` | string (pk) | 내부 표준 ID |
| `isActive` | bool | 운영 토글 |
| `appleLeaderboardId` | string | Game Center ID |
| `googleLeaderboardId` | string | GPGS ID |
| `scoreOrder` | enum: `LEADERBOARD_SCORE_ORDER` | `HighBetter` / `LowBetter` |

제약:
- `isActive=true` 행은 타겟 플랫폼 ID를 반드시 채운다.
- 플랫폼 ID는 매핑 전용 데이터이며 외부 API payload에 노출하지 않는다.
