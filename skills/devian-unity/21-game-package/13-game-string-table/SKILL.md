# 13-game-string-table

Status: ACTIVE
AppliesTo: v10

**GamePackage 전용 String Table 규약.**
`GameStringTable.xlsx`에서 `GAME_TEXT`를 생성하고, Unity에서는 `GAME_TEXT_ID` + `ST_GAME_TEXT`로 소비한다.

---

## 1. Purpose

- Common의 canonical `TEXT`를 대체하지 않는다
- Game 전용 localization key를 별도 StringTable로 관리한다
- `UIPackage`의 `UIComponentText`는 이 스킬 기준으로 `GAME_TEXT_ID`를 사용한다

---

## 2. Input Files

### Build Input JSON

`domains.Game`에 String Table 입력을 추가한다:

```json
"Game": {
  "type": "client",
  "contractDir": "Domains/Game",
  "contractFiles": ["*.json"],
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

### XLSX Path

- `input/Domains/Game/GameStringTable.xlsx`

### Sheet Name

- `GAME_TEXT`

`GAME_TEXT`는 전역 `TEXT`와 다른 이름이므로 string output collision을 피한다.

---

## 3. Sheet Schema

헤더는 Common StringTable 규약과 동일하다:

| 컬럼 | 필수 | 설명 |
|---|---|---|
| `id` | ✅ | Game text key |
| `description` | ✅ | 설명/메모 |
| `English` | ✅ | 영문 |
| `Korean` | 선택 | 한국어 |
| `Japanese` | 선택 | 일본어 |

언어 컬럼 규칙, ndjson/pb64 인코딩, Addressables key 규칙은 [devian-unity/20-common-package/30-string-table](../../20-common-package/30-string-table/SKILL.md)가 정본이다.

---

## 4. Generated Outputs

### C# Domain Module

- `framework-cs/module/Devian.Domain.Game/Generated/Game.g.cs`
  - `GAME_TEXT_ID`

### Unity GamePackage

- `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/Generated/ST_GAME_TEXT.g.cs`
- `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Runtime/Generated/DomainTableRegistry.g.cs`
- `framework-cs/upm/com.devian.foundation/Samples~/GamePackage/Editor/Generated/GAME_TEXT_ID.Editor.cs`

### Final String Assets

- `framework-cs/apps/UnityExample/Assets/Bundles/Strings/ndjson/{Language}/GAME_TEXT.json`
- `framework-cs/apps/UnityExample/Assets/Bundles/Strings/pb64/{Language}/GAME_TEXT.asset`

---

## 5. Runtime Consumption

### Namespace

- `Devian.Domain.Game`

### API

- key type: `GAME_TEXT_ID`
- lookup wrapper: `ST_GAME_TEXT.Get(id)`

### UIPackage Integration

`UIComponentText`는 `GAME_TEXT_ID` serialized field를 사용하고, 내부 lookup은 `ST_GAME_TEXT.Get(...)`를 호출한다.

---

## 6. Hard Rules

- Common `TEXT`는 유지한다. 직접 rename/move 금지.
- Game 전용 StringTable 이름은 `GAME_TEXT`처럼 고유해야 한다.
- `GameStringTable.xlsx`가 `Domains/Game/` 루트에 있으면 `tableFiles`는 wildcard가 아니라 명시적 파일 목록이어야 한다.
- `stringDir/stringFiles`와 `tableDir/tableFiles`가 overlap 되면 빌드 FAIL이다.
- `GAME_TEXT_ID`를 쓰는 소비 코드는 `Devian.Domain.Game`를 참조해야 한다.

---

## 7. Related

- [00-overview](../00-overview/SKILL.md) — GamePackage 개요
- [12-game-ability](../12-game-ability/SKILL.md) — GamePackage 수동 addon 코드
- [devian/21-domain-game/11-game-tables](../../../devian/21-domain-game/11-game-tables/SKILL.md) — Game 입력/생성물
- [devian-unity/20-common-package/30-string-table](../../20-common-package/30-string-table/SKILL.md) — 공통 StringTable SSOT
- [devian-unity/23-ui-package/20-ui-components/12-ui-component-text](../../23-ui-package/20-ui-components/12-ui-component-text/SKILL.md) — `GAME_TEXT_ID` 소비자
