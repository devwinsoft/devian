# 11-game-tables

Status: ACTIVE
AppliesTo: v10

**Game 도메인 테이블 정의.** DomainKey = `Game`의 테이블(XLSX) 입력과 데이터 생성물을 정의한다.

---

## 1. Input JSON (`{buildInputJson}` 예시)

`domains.Game` 테이블 설정:

```json
"Game": {
  "tableDir": "Domains/Game",
  "tableFiles": ["*.xlsx"]
}
```

| 필드 | 값 | 설명 |
|---|---|---|
| `tableDir` | `Domains/Game` | 테이블 XLSX 디렉토리 |
| `tableFiles` | `["*.xlsx"]` | 테이블 파일 패턴 |

---

## 2. Tables (`input/Domains/Game/`)

| 파일 | 시트(테이블) | 컨테이너 | PK | 설명 |
|---|---|---|---|---|
| `MetaTable.xlsx` | PURCHASE | TB_PURCHASE | `InternalProductId` (string) | 상품 테이블 (`seasonId` 포함) |
| `MetaTable.xlsx` | ADVERTISE | TB_ADVERTISE | `AdvertiseId` (string) | 광고 placement 테이블 |
| `MetaTable.xlsx` | REWARD | TB_REWARD | `RewardNum` (int) | 보상 테이블 |
| `GameMessageTable.xlsx` | GAME_MESSAGE | TB_GAME_MESSAGE | `MessageId` (string) | 메시지 stat 정의 테이블 |
| `MissionTable.xlsx` | MISSION_DAILY | TB_MISSION_DAILY | `MissionId` (string) | 일일 미션 정의 테이블 |
| `MissionTable.xlsx` | MISSION_PERIOD | TB_MISSION_PERIOD | `MissionId` (string) | 기간 미션 정의 테이블 (`day:1~7`, 10일 주기) |
| `AchieveTable.xlsx` | ACHIEVE_ONCE | TB_ACHIEVE_ONCE | `Index` (int) | 일반 업적 단계/플랫폼 매핑 테이블 |
| `AchieveTable.xlsx` | ACHIEVE_PASS | TB_ACHIEVE_PASS | `Index` (int) | 패스 업적 단계/활성 조건 테이블 |
| `MetaTable.xlsx` | LEADERBOARD | TB_LEADERBOARD | `LeaderboardId` (string) | 리더보드 정의 |
| `MetaTable.xlsx` | LEADERBOARD_REWARD | TB_LEADERBOARD_REWARD | `Index` (int) | 리더보드 구간 보상 정의 |
| `TreasureTable.xlsx` | TREASURE_CHEST | TB_TREASURE_CHEST | `treasuerGradeType` (`TREASURE_GRADE_TYPE`) | treasure chest 등급별 보상 그룹 엔트리 |
| `TreasureTable.xlsx` | TREASURE_PROGRESS | TB_TREASURE_PROGRESS | `Level` (int) | progress level별 maxExp/보상 그룹 엔트리 |
| `TreasureTable.xlsx` | TREASURE_GROUP | TB_TREASURE_GROUP | `Index` (int) | `treasureGroupId -> rewardGroupId` fan-out 테이블 |
| `ItemTable.xlsx` | ITEM_EQUIP | TB_ITEM_EQUIP | `EquipId` (string) | 장비 테이블 (EquipId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_CARD | TB_ITEM_CARD | `CardId` (string) | 카드 테이블 (CardId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_RENTAL | TB_ITEM_RENTAL | `RentalId` (string) | 렌탈 아이템 테이블 (RentalId, NameId, DescId) |
| `ItemTable.xlsx` | ITEM_PASS | TB_ITEM_PASS | `PassId` (string) | 패스 아이템 테이블 (PassId, NameId, DescId) |
| `MetaTable.xlsx` | SEASON | TB_SEASON | `SeasonId` (string) | 시즌 메타 테이블 (start/end UTC) |

---

## 3. Generated Data

| 형식 | 생성물 | 경로 |
|---|---|---|
| ndjson | `*.json` | `{tableConfig.tableDirs}/ndjson/` |
| pb64 | `*.asset` | `{tableConfig.tableDirs}/pb64/` |

빌드 실행: `bash input/build.sh {buildInputJson}` (예: `bash input/build.sh input/build_input.json`)

---

## 4. Related

| 주제 | 스킬 |
|---|---|
| Builder SSOT (테이블 규칙) | [devian/80-tools/11-builder/03-ssot](../../80-tools/11-builder/03-ssot/SKILL.md) |
| Examples SSOT (config/input) | [devian-examples/03-ssot](../../../devian-examples/03-ssot/SKILL.md) |
| Root SSOT (용어/경로) | [devian/10-module/03-ssot](../../10-module/03-ssot/SKILL.md) |
| 21-domain-game 개요 | [00-overview](../00-overview/SKILL.md) |
