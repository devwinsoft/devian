# Devian v10 — Table Data Export (NDJSON + Unity Asset)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

XLSX 테이블에서 추출된 데이터를 **NDJSON(Line-delimited JSON)**과 **Unity TextAsset `.asset`** 파일로 내보내는 규약을 정의한다.

이 문서는 "산출 포맷과 경로"만 정의한다.
생성되는 로더/컨테이너 API는 **런타임/제너레이터 코드**가 정답이다.

---

## NDJSON 규약

- 파일은 UTF-8 텍스트
- 한 줄이 한 레코드(JSON object)
- 빈 줄은 허용하지 않는 것을 권장
- 각 row의 필드 순서는 Excel 컬럼 순서를 따름
- 64-bit integers(`long`, `ulong`)는 JSON string으로 변환

> JSON array가 아니라 NDJSON이 기본 산출물이다.

### 확장자 규약 (중요)

**파일 확장자는 `.json`이지만, 파일 내용은 NDJSON(라인 단위 JSON)이다.**

- 확장자는 소비 측(Unity/툴링) 요구로 `.json`을 사용한다.
- 파일명: `{TableName}.json`
- 출력 폴더: `ndjson/` (bin/ 폴더와 혼동 금지)

---

## Data Export 생성 조건

**PrimaryKey(`pk`)가 없는 sheet는 NDJSON/bin을 생성하지 않는다.**

- PrimaryKey가 있는 sheet → `ndjson/{SheetName}.json` + `bin/{SheetName}.asset` 생성
- PrimaryKey가 없는 sheet → Data export 안함 (Entity/Container 코드만 생성)

이 규칙은 "데이터 없이 스키마만 정의하는 sheet"를 허용하기 위함이다.

---

## PK Validation (Export 필터링 규칙)

**DATA export는 PK 유효 row만 포함하며, 유효 row가 없으면 파일을 생성하지 않는다.**

### NDJSON 규칙

1. `primaryKey`(`pk` 옵션)가 정의되지 않은 테이블은 ndjson 파일을 **생성하지 않는다**
2. `primaryKey` 값이 빈 row(null, undefined, "")는 export 대상에서 **제외된다**
3. export 가능한 row가 0개면 파일을 **생성하지 않는다**

### bin 규칙 (테이블 단위 스킵)

1. `primaryKey`(`pk` 옵션)가 정의되지 않은 테이블은 bin 파일을 **생성하지 않는다**
2. row 중 `primaryKey` 값이 빈 것이 **하나라도** 있으면 **테이블 전체를 스킵**한다
3. 로그: `[Skip] Asset export skipped (empty PK row): <TableName>`

> bin은 테이블 단위 1개 파일이므로, row 일부만 빼면 데이터 불일치가 생긴다. 따라서 "테이블 전체 스킵"으로 고정.

### 로그

- PK 미정의: `[Skip] Table export skipped (no primaryKey defined): <SheetName>`
- 유효 row 없음: `[Skip] Table export skipped (no valid PK rows): <SheetName>`
- 빈 PK row 존재 (bin): `[Skip] Asset export skipped (empty PK row): <TableName>`

### 주의

- **빌드 실패(throw)가 아니라 스킵**이다. 다른 테이블은 정상 처리된다.

---

## bin export 규칙 (Unity TextAsset YAML)

**pk 옵션이 있는 테이블만 Unity TextAsset `.asset` 파일로 export한다.**

### 적용 대상

- pk 옵션(`table.keyField` 존재)이 있는 테이블만 `bin/` 폴더에 `.asset` 파일 생성
- pk 옵션이 없는 테이블은 bin export 안함

### 파일 생성 규칙

- **테이블 단위** 파일 생성 (row 단위 X)
- 파일명: `{TableName}.asset` (테이블 이름)
- 출력 경로: `{dataTargetDir}/{DomainKey}/bin/{TableName}.asset`

### Unity TextAsset YAML 구조

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!49 &4900000
TextAsset:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: <TABLE_NAME>
  m_Script: <BASE64>
```

- `m_Name`: 테이블 이름과 동일
- `m_Script`: pb64 binary (base64 인코딩)

### m_Script 바이너리 포맷 (pb64)

base64 디코드 후 구조 (테이블의 모든 row를 concat):

```
[row1][row2][row3]...
```

각 row 구조:
```
[varint jsonLength][jsonUtf8]
```

- `jsonLength`: JSON 문자열의 UTF-8 바이트 길이 (protobuf varint 인코딩)
- `jsonUtf8`: JSON 문자열의 UTF-8 바이트

### Varint 인코딩

protobuf 표준 varint 인코딩을 사용한다:
- unsigned 32-bit 길이 기준
- 각 바이트의 MSB가 1이면 다음 바이트가 이어짐
- 7비트씩 little-endian 순서로 저장

### 결정성 (Deterministic)

**같은 입력(XLSX)이면 항상 같은 .asset 출력이 나와야 한다.**

- 필드 순서: Excel column 순서
- JSON serialization: 컴팩트 포맷 (들여쓰기 없음)

---

## Output Paths

SSOT 경로 규약을 따른다. (`{dataTargetDirs}`는 배열이므로 각 요소에 대해 복사)

- staging:
  - `{tempDir}/{DomainKey}/data/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{tempDir}/{DomainKey}/data/bin/{TableName}.asset` (pk 옵션 있는 테이블만, 내용은 pb64 YAML)
- final (각 `{dataTargetDir}` 요소에 대해):
  - `{dataTargetDir}/{DomainKey}/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{dataTargetDir}/{DomainKey}/bin/{TableName}.asset` (pk 옵션 있는 테이블만, 내용은 pb64 YAML)

---

## 금지 행동

- NDJSON 내용을 JSON 배열(`[]`)로 바꾸는 행위 금지 (NDJSON 형식 유지)
- `ndjson/` 폴더를 `json/` 폴더로 rename 하는 행위 금지 (폴더명은 `ndjson` 유지)
- pk 옵션이 없는 테이블을 export하도록 완화하는 행위 금지
- 특정 테이블명(ASSET 등)에 의존하는 행위 금지

---

## Notes

- `enum:*` / `class:*` 컬럼의 셀 값은 DFF 원문 문자열로 보존될 수 있다.
- DFF 문법은 `skills/devian/25-class-cell-format/SKILL.md`를 따른다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- DFF 규약: `skills/devian/25-class-cell-format/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
