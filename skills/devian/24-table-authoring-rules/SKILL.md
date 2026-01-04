# Devian v10 — Table Authoring Rules (XLSX)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

DATA(DomainType=DATA)에서 사용하는 XLSX 테이블 작성 규칙을 정의한다.

이 문서는 **XLSX 헤더/옵션/중단 규칙**만을 정본으로 가진다.
지원 타입의 상세 매핑과 생성 코드 구조는 **`docs/generated/devian-reference.md`**를 정답으로 본다.

---

## Inputs

입력은 build.json의 `domains` 섹션이 정본이다.

- `domains[{DomainKey}].tablesDir`
- `domains[{DomainKey}].tableFiles` (예: `['*.xlsx']`)

---

## XLSX Sheet → Table

- XLSX의 각 sheet는 하나의 table로 해석한다.
- table 이름은 sheet 이름에서 파생되며, 정확한 정규화 규칙은 Reference를 따른다.

---

## Header Layout (4 rows)

모든 sheet는 최소 4행 헤더를 가진다.

| Row | 의미 | 규칙 |
|---:|---|---|
| 1 | Field Names | 빈 셀을 만나면 그 뒤 컬럼은 무시(Header Stop Rule) |
| 2 | Types | Row1에서 유효한 컬럼 범위 내에서만 읽음 |
| 3 | Options | Row1에서 유효한 컬럼 범위 내에서만 읽음 |
| 4 | Comments | 현재 빌드 도구는 해석하지 않음 |

Row 5부터 데이터다.

---

## Options

옵션은 `key:value` 쌍을 `,`로 구분하는 문자열이다.

예:

```
key:true, optional:true
```

해석 규칙:

- `key:true` → PrimaryKey
- `optional:true` → nullable/optional 힌트
- 그 외 키(`parser:*` 포함)는 **Reserved**
  - 존재해도 의미를 부여하지 않는다(강제/필수 해석 금지)

PrimaryKey 규칙:

- PrimaryKey는 0개 또는 1개만 허용
- 2개 이상이면 빌드 실패

---

## Data Rules

### Header Stop Rule

- Row1(Field Names)에서 빈 셀을 만나면 **그 뒤 컬럼은 무시**한다.

### Data Stop Rule

- PrimaryKey 컬럼이 빈 값이면 **즉시 중단**한다.
- 이후 행은 읽지 않는다.

---

## Type Strings

타입 문자열은 Row2에서 지정한다.

- 스칼라 타입(예: `int`, `string` 등)
- 배열 타입(예: `int[]`)
- 참조 타입(예: `enum:...`, `class:...`)

지원 타입 문자열과 C#/TS 매핑은 Reference가 정답이다.

참조 타입 셀 값의 텍스트 규약(DFF)은 `skills/devian/25-class-cell-format/SKILL.md`를 따른다.

---

## Outputs

경로 규약은 SSOT를 따른다.

- staging: `{tempDir}/{DomainKey}/cs/generated/**`, `{tempDir}/{DomainKey}/ts/generated/**`, `{tempDir}/{DomainKey}/data/json/**.ndjson`
- final: `{csTargetDir}/generated/**`, `{tsTargetDir}/generated/**`, `{dataTargetDir}/json/**`

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- DFF 규약: `skills/devian/25-class-cell-format/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`
