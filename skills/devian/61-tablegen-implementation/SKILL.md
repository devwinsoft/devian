# Devian – 61 Tablegen Implementation

## Purpose

**XLSX 테이블을 JSON 데이터로 변환하고, sheet 단위 meta를 생성한다.**

---

## Belongs To

**Consumer / Tooling (Build tool)**

---

## 공통 핵심 원칙 (최우선)

| # | 원칙 |
|---|------|
| 1 | **Devian은 비즈니스 로직을 다루지 않는다** |
| 2 | **sub-domain / language / region / shard 개념을 해석하지 않는다** |
| 3 | **{table}@{sub} 같은 키의 의미는 개발자 정책이다** |
| 4 | **테이블 종류 1개당 컨테이너는 정확히 1개** |

---

## Sheet / Header / Data 규칙

### Excel 구조

```
ExcelFile 내 각 Sheet = 1 Table
```

### Header 규칙 (3줄 고정)

| 행 | 내용 | 비고 |
|----|------|------|
| **Row 1** | Field Name | 필드 이름 |
| **Row 2** | Type | prefix 지원 |
| **Row 3** | Comment | 순수 설명용 주석 — **Devian은 절대 해석하지 않음**, JSON 미포함 |
| **Row 4+** | Data | 실제 데이터 |

> **IMPORTANT:**
> - **Row 3에 meta/option/policy/constraint 개념은 없다**
> - **tablegen은 Row 3을 로직에 사용하지 않는다** (단순 저장만 가능)
> - **입력이 3줄 헤더가 아니면 빌드 실패**

---

## Primary Key 규칙

| # | Rule |
|---|------|
| 1 | **Primary Key는 무조건 1열** |
| 2 | 별도 표기 없음 (1열이 자동으로 Key) |
| 3 | **Key 타입 제한: `class:*` 금지** |
| 4 | **Key 타입 제한: 배열 타입 금지** |
| 5 | 허용: `int`, `string`, `enum:*` 등 |

---

## Type 규칙 (접두어 방식)

### 지원되는 타입

| 형식 | 설명 |
|------|------|
| `int` | 정수 |
| `float` | 실수 |
| `string` | 문자열 |
| `bool` | 불리언 |
| `datetime` | 날짜/시간 |
| `text` | 긴 텍스트 |
| `int[]`, `string[]` | 배열 |
| `enum:Name` | 열거형 (contracts 참조) |
| `class:Name` | 클래스 (contracts 참조) |
| `ref:Name` | **예약됨 (Reserved)** |

### ref: 정책 (MUST)

```
ref:* 발견 시 빌드 실패
에러 메시지에 반드시 "planned / not supported yet" 포함
```

```
Error: ref type is planned but not supported yet: ref:ItemId (Sheet=Items, Column=C)
```

---

## Comment 정책 (MUST)

| # | Rule |
|---|------|
| 1 | **3행 Comment는 순수 설명용 주석이다** |
| 2 | **Devian은 3행을 절대 해석하지 않는다** |
| 3 | **데이터 JSON 산출물에 포함하지 않는다** |
| 4 | **meta에 comment 문자열을 저장할 수는 있으나, 로직에 사용하지 않는다** |
| 5 | **3행에 meta/option/policy/constraint 개념은 없다** |

---

## sub-domain / language 정책

| # | Rule |
|---|------|
| 1 | **Devian은 `@`, `_KR`, `_JP` 등을 해석하지 않는다** |
| 2 | 단순 문자열로 취급 |
| 3 | 의미 부여는 개발자 책임 |

---

## 산출물

### Sheet 단위 데이터 JSON

```
{DATA_OUT}/{domain}/{sheetKey}.json
```

### Sheet 단위 meta (67에서 사용)

```
.devian/work/tables/{domain}/{ExcelFileName}.{SheetName}.tablemeta.json
```

### meta 최소 필드

```json
{
  "domain": "common",
  "excelFileName": "A",
  "sheetName": "Items",
  "tableName": "Items",
  "sheetKey": "items",
  "rowTypeName": "ItemsRow",
  "primaryKeyColumnIndex": 0,
  "primaryKeyFieldName": "id",
  "primaryKeyTypeKey": "int",
  "columns": [
    { "name": "id", "type": "int", "comment": "아이템 ID" },
    { "name": "name", "type": "string", "comment": "이름" }
  ]
}
```

---

## sheetKey 정규화 규칙

```
function normalizeSheetKey(sheetName):
  result = sheetName.trim()
  result = result.toLowerCase()
  result = result.replace(/[\s\-]+/g, '_')
  return result
```

| SheetName | sheetKey |
|-----------|----------|
| `Items` | `items` |
| `Item Stats` | `item_stats` |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | ExcelFile 내 각 Sheet = Table로 처리 |
| 2 | Header 3행 고정, 데이터 4행부터 |
| 3 | Primary Key는 무조건 1열 |
| 4 | **ref:* 발견 시 빌드 실패** (에러에 "planned" 포함) |
| 5 | Comment는 JSON 미포함 |
| 6 | **sub-domain 해석 안 함** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `67-table-loader-codegen` | meta 소비자 |
| `65-table-loader-implementation` | 런타임 사용법 |
| `24-table-authoring-rules` | 테이블 작성 규칙 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.9.0 | 2024-12-25 | **3줄 헤더 정책 확정**: Row 3은 순수 comment, Devian은 절대 해석하지 않음 |
| 0.8.0 | 2024-12-25 | 최종 통합 지시서 반영, sub-domain 해석 금지 명시 |
| 0.7.0 | 2024-12-25 | meta에 primaryKey* 필드 추가 |
| 0.6.0 | 2024-12-21 | 3행 헤더, type prefix, ref 예약 |
| 0.1.0 | 2024-12-21 | Initial skill definition |
