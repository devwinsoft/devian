# 50-operation/20-excel-reward-id-export — Reward ID Catalog Import


Status: ACTIVE
AppliesTo: v10


## Purpose

`ENUM_TYPES.json` / `ItemTable.xlsx` / `UnitTable.xlsx`에서
`CURRENCY`/`EQUIP`/`CARD`/`HERO` id를 추출해
Firestore `/config/rewardIdCatalog`로 import한다.

Initial Inventory 탭의 `RewardData.id` listbox 데이터 원천이다.


---


## Input Source (XLSX)

- `.env`에서 입력 xlsx 경로를 설정한다.
  - `OP_REWARD_ENUM_TYPES_JSON_PATH` (기본값: `input/Domains/Game/ENUM_TYPES.json`)
  - `OP_REWARD_ITEM_TABLE_XLSX_PATH` (기본값: `input/Domains/Game/ItemTable.xlsx`)
  - `OP_REWARD_UNIT_TABLE_XLSX_PATH` (기본값: `input/Domains/Game/UnitTable.xlsx`)

- 파일: `OP_REWARD_ENUM_TYPES_JSON_PATH`
  - enum: `CURRENCY_TYPE`
  - 값: `values[].name`
- 파일: `OP_REWARD_ITEM_TABLE_XLSX_PATH`
  - 시트: `EQUIP`
  - 컬럼: `equipId`
- 파일: `OP_REWARD_ITEM_TABLE_XLSX_PATH`
  - 시트: `CARD`
  - 컬럼: `cardId`
- 파일: `OP_REWARD_UNIT_TABLE_XLSX_PATH`
  - 시트: `UNIT_HERO`
  - 컬럼: `unitId`

Devian table 관례:
- row 1: header
- row 2: type
- row 3: options
- row 4: description
- row 5+: data


---


## Firestore Target

- 문서: `/config/rewardIdCatalog`
- payload:

```json
{
  "currencyIds": ["GOLD"],
  "equipIds": ["equip_sword_001"],
  "cardIds": ["card_fire_001"],
  "heroIds": ["hero_knight_001"],
  "importedAt": "2026-03-06T00:00:00.000Z",
  "source": {
    "enumTypesPath": "input/Domains/Game/ENUM_TYPES.json",
    "itemTablePath": "input/Domains/Game/ItemTable.xlsx",
    "unitTablePath": "input/Domains/Game/UnitTable.xlsx"
  }
}
```


---


## Script

- 경로: `framework-ts/apps/Operation/scripts/import-reward-id-catalog.mjs`
- env 키:
  - `OP_REWARD_ENUM_TYPES_JSON_PATH`
  - `OP_REWARD_ITEM_TABLE_XLSX_PATH`
  - `OP_REWARD_UNIT_TABLE_XLSX_PATH`

실행:

```bash
cd framework-ts/apps/Operation
node scripts/import-reward-id-catalog.mjs
```

dry-run:

```bash
cd framework-ts/apps/Operation
node scripts/import-reward-id-catalog.mjs --dry-run
```


---


## Validation Checklist

- script 실행 후 `/config/rewardIdCatalog` 문서가 갱신된다.
- Initial Inventory 탭에서:
  - `CURRENCY` 선택 시 id listbox에 `CURRENCY_TYPE` 값이 보인다.
  - `EQUIP`/`CARD`/`HERO` 선택 시 id listbox에 import된 값이 보인다.
  - listbox가 비어 있으면 add 버튼이 비활성화된다.


---


## Related

- [19-page-initial-inventory](../19-page-initial-inventory/SKILL.md) — Initial Inventory 탭 UI
- [03-ssot](../03-ssot/SKILL.md) — Operation 탭 정본
- [49-reward-system/11-rewarddata-interpretation](../../../../devian-unity/50-mobile-system/49-reward-system/11-rewarddata-interpretation/SKILL.md)
