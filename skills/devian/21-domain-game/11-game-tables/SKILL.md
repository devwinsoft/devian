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
  "tableFiles": [
    "AchieveTable.xlsx",
    "GameMessageTable.xlsx",
    "ItemTable.xlsx",
    "MetaTable.xlsx",
    "MissionTable.xlsx",
    "ShopTable.xlsx",
    "TreasureTable.xlsx",
    "UnitTable.xlsx"
  ],
  "stringDir": "Domains/Game",
  "stringFiles": ["GameStringTable.xlsx"]
}
```

| 필드 | 값 | 설명 |
|---|---|---|
| `tableDir` | `Domains/Game` | 테이블 XLSX 디렉토리 |
| `tableFiles` | 명시적 8개 테이블 파일 목록 | `GameStringTable.xlsx`와 overlap을 피하는 테이블 입력 목록 |
| `stringDir` | `Domains/Game` | Game StringTable XLSX 디렉토리 |
| `stringFiles` | `["GameStringTable.xlsx"]` | Game StringTable 파일 패턴 |

---

## 2. Tables (`input/Domains/Game/`)

| 파일 | 시트(테이블) | 컨테이너 | PK | 설명 |
|---|---|---|---|---|
| `MetaTable.xlsx` | PURCHASE | TB_PURCHASE | `Internal_product_id` (string) | 상품 테이블 (`season_id` 포함) |
| `MetaTable.xlsx` | ADVERTISE | TB_ADVERTISE | `Advertise_id` (string) | 광고 placement 테이블 |
| `MetaTable.xlsx` | REWARD | TB_REWARD | `Reward_num` (int) | 보상 테이블 |
| `GameMessageTable.xlsx` | GAME_MESSAGE | TB_GAME_MESSAGE | `Message_id` (string) | 메시지 stat 정의 테이블 |
| `MissionTable.xlsx` | MISSION_DAILY | TB_MISSION_DAILY | `Mission_id` (string) | 일일 미션 정의 테이블 |
| `MissionTable.xlsx` | MISSION_WEEKLY | TB_MISSION_WEEKLY | `Mission_id` (string) | 기간 미션 정의 테이블 (`day:1~7`, 10일 주기) |
| `AchieveTable.xlsx` | ACHIEVE_SOCIAL | TB_ACHIEVE_SOCIAL | `Index` (int) | 소셜 업적 단계/플랫폼 매핑 테이블 |
| `AchieveTable.xlsx` | ACHIEVE_PASS | TB_ACHIEVE_PASS | `Index` (int) | 패스 업적 단계/활성 조건 테이블 |
| `MetaTable.xlsx` | LEADERBOARD | TB_LEADERBOARD | `Leaderboard_id` (string) | 리더보드 정의 |
| `MetaTable.xlsx` | LEADERBOARD_REWARD | TB_LEADERBOARD_REWARD | `Index` (int) | 리더보드 구간 보상 정의 |
| `TreasureTable.xlsx` | TREASURE_CHEST | TB_TREASURE_CHEST | `Level` (int) | chest level별 max_exp/보상 그룹 엔트리 |
| `TreasureTable.xlsx` | TREASURE_REWARD | TB_TREASURE_REWARD | `Index` (int) | `treasure_grade_type -> reward_group_id` fan-out 테이블 |
| `ItemTable.xlsx` | ITEM_EQUIP | TB_ITEM_EQUIP | `item_id` (string) | 장비 테이블 (`item_id`, `name_id`, `desc_id`) |
| `ItemTable.xlsx` | ITEM_CARD | TB_ITEM_CARD | `item_id` (string) | 카드 테이블 (`item_id`, `name_id`, `desc_id`) |
| `ItemTable.xlsx` | ITEM_MATERIAL | TB_ITEM_MATERIAL | `item_id` (string) | 재료 테이블 (`item_id`, `name_id`, `desc_id`) |
| `ItemTable.xlsx` | ITEM_RENTAL | TB_ITEM_RENTAL | `item_id` (string) | 렌탈 아이템 테이블 (`item_id`, `name_id`, `desc_id`) |
| `ItemTable.xlsx` | ITEM_PASS | TB_ITEM_PASS | `item_id` (string) | 패스 아이템 테이블 (`item_id`, `name_id`, `desc_id`) |
| `MetaTable.xlsx` | SEASON | TB_SEASON | `Season_id` (string) | 시즌 메타 테이블 (start/end UTC) |

---

## 3. String Tables (`input/Domains/Game/`)

| 파일 | 시트(StringTable) | 생성 ID 타입 | 생성 Wrapper | 설명 |
|---|---|---|---|---|
| `GameStringTable.xlsx` | `GAME_TEXT` | `GAME_TEXT_ID` | `ST_GAME_TEXT` | Game 전용 로컬라이즈 문자열 |

`GAME_TEXT`는 Common의 canonical `TEXT`와 별도다.
`GameStringTable.xlsx`가 `Domains/Game/` 루트에 있으므로 `tableFiles`는 wildcard가 아니라 명시적 목록이어야 한다.

---

## 4. Generated Data

| 형식 | 생성물 | 경로 |
|---|---|---|
| ndjson | `*.json` | `{tableConfig.tableDirs}/ndjson/` |
| pb64 | `*.asset` | `{tableConfig.tableDirs}/pb64/` |
| string ndjson | `GAME_TEXT.json` | `{tableConfig.stringDirs}/ndjson/{Language}/` |
| string pb64 | `GAME_TEXT.asset` | `{tableConfig.stringDirs}/pb64/{Language}/` |

빌드 실행: `bash input/build.sh {buildInputJson}` (예: `bash input/build.sh input/build_input.json`)

---

## 5. Related

| 주제 | 스킬 |
|---|---|
| Builder SSOT (테이블 규칙) | [devian/80-tools/11-builder/03-ssot](../../80-tools/11-builder/03-ssot/SKILL.md) |
| Examples SSOT (config/input) | [devian-examples/03-ssot](../../../devian-examples/03-ssot/SKILL.md) |
| Root SSOT (용어/경로) | [devian/10-module/03-ssot](../../10-module/03-ssot/SKILL.md) |
| 21-domain-game 개요 | [00-overview](../00-overview/SKILL.md) |
| GamePackage StringTable | [devian-unity/21-game-package/13-game-string-table](../../../devian-unity/21-game-package/13-game-string-table/SKILL.md) |
