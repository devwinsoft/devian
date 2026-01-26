# 32-json-row-io

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

XLSX 테이블에서 추출된 데이터를 JSON object로 내보낼 때의 규칙(필드 순서, 64-bit int string 처리, PK export 필터)을 정의한다.

이 문서는 "row → JSON 변환 규칙"만 정의한다.

- **NDJSON 저장 규약**: `skills/devian/34-ndjson-storage/SKILL.md` 참조
- **pb64 저장 규약**: `skills/devian/35-pb64-storage/SKILL.md` 참조

생성되는 로더/컨테이너 API는 **런타임/제너레이터 코드**가 정답이다.

---

## Row → JSON 변환 규칙

### 필드 순서

**각 row의 필드 순서는 Excel 컬럼 순서를 따른다.**

### 64-bit Integer 처리

**64-bit integers(`long`, `ulong`)는 JSON string으로 변환한다.**

이유: JavaScript의 Number 타입은 53비트 정밀도까지만 안전하므로, 64비트 정수는 문자열로 변환하여 정밀도 손실을 방지한다.

### JSON Serialization

- 컴팩트 포맷 (들여쓰기 없음)
- 결정성: 같은 입력이면 같은 출력

---

## Data Export 생성 조건

**PrimaryKey(`pk`)가 없는 sheet는 NDJSON/pb64을 생성하지 않는다.**

- PrimaryKey가 있는 sheet → `ndjson/{SheetName}.json` + `pb64/{SheetName}.asset` 생성
- PrimaryKey가 없는 sheet → Data export 안함 (Entity/Container 코드만 생성)

이 규칙은 "데이터 없이 스키마만 정의하는 sheet"를 허용하기 위함이다.

---

## PK Validation (Export 필터링 규칙)

**DATA export는 PK 유효 row만 포함하며, 유효 row가 없으면 파일을 생성하지 않는다.**

### NDJSON 규칙

1. `primaryKey`(`pk` 옵션)가 정의되지 않은 테이블은 ndjson 파일을 **생성하지 않는다**
2. `primaryKey` 값이 빈 row(null, undefined, "")는 export 대상에서 **제외된다**
3. export 가능한 row가 0개면 파일을 **생성하지 않는다**

### pb64 규칙 (테이블 단위 스킵)

1. `primaryKey`(`pk` 옵션)가 정의되지 않은 테이블은 pb64 파일을 **생성하지 않는다**
2. row 중 `primaryKey` 값이 빈 것이 **하나라도** 있으면 **테이블 전체를 스킵**한다
3. 로그: `[Skip] Asset export skipped (empty PK row): <TableName>`

> pb64은 테이블 단위 1개 파일이므로, row 일부만 빼면 데이터 불일치가 생긴다. 따라서 "테이블 전체 스킵"으로 고정.

### 로그

- PK 미정의: `[Skip] Table export skipped (no primaryKey defined): <SheetName>`
- 유효 row 없음: `[Skip] Table export skipped (no valid PK rows): <SheetName>`
- 빈 PK row 존재 (pb64): `[Skip] Asset export skipped (empty PK row): <TableName>`

### 주의

- **빌드 실패(throw)가 아니라 스킵**이다. 다른 테이블은 정상 처리된다.

---

## Output Paths

SSOT 경로 규약을 따른다. (`{dataConfig.bundleDirs}`는 배열이므로 각 요소에 대해 복사)

- staging:
  - `{tempDir}/{DomainKey}/data/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{tempDir}/{DomainKey}/data/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)
- final (각 `{bundleDir}` 요소에 대해):
  - `{bundleDir}/Tables/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{bundleDir}/Tables/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)

---

## 금지 행동

- pk 옵션이 없는 테이블을 export하도록 완화하는 행위 금지
- 특정 테이블명(ASSET 등)에 의존하는 행위 금지
- 필드 순서 규칙 변경 금지

---

## Notes

- `enum:*` / `class:*` 컬럼의 셀 값은 DFF 원문 문자열로 보존될 수 있다.
- DFF 문법은 `skills/devian/31-class-cell-format/SKILL.md`를 따른다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- NDJSON 저장: `skills/devian/34-ndjson-storage/SKILL.md`
- pb64 저장: `skills/devian/35-pb64-storage/SKILL.md`
- DFF 규약: `skills/devian/31-class-cell-format/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
