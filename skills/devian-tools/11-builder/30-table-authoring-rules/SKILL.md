# 30-table-authoring-rules

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

DATA(DomainType=DATA)에서 사용하는 XLSX 테이블 작성 규칙을 정의한다.

이 문서는 **XLSX 헤더/옵션/중단 규칙**만을 정본으로 가진다.
지원 타입의 상세 매핑과 생성 코드 구조는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Inputs

입력은 `{buildInputJson}`의 `domains` 섹션이 정본이다.

- `domains[{DomainKey}].tableDir`
- `domains[{DomainKey}].tableFiles` (예: `['*.xlsx']`)

---

## XLSX Sheet → Table

- XLSX의 각 sheet는 하나의 table로 해석한다.
- table 이름은 sheet 이름에서 파생되며, 정확한 정규화 규칙은 Reference를 따른다.

---

## Sheet Name 규칙 (Hard Rule)

Sheet 이름은 다음 형식을 지원한다:

| 형식 | 예시 | CodeTableName |
|------|------|---------------|
| `{TableName}` | `TestSheet` | `TestSheet` |
| `{TableName}@{Description}` | `Monsters@몬스터테이블` | `Monsters` |

### 파싱 규칙

- `@` 문자가 있으면 앞쪽 문자열이 **CodeTableName**
- `@` 뒤 문자열은 설명용 (Description)
- 코드 생성/런타임에서 사용되는 이름은 **CodeTableName**만

### 런타임 동작

- `{TableName}@{Description}` 형식의 에셋이 로드되면 `TB_{CodeTableName}`에 자동 insert
- TableManager가 `ExtractBaseName(fileName)`으로 CodeTableName 추출

### 금지 규칙

- `@` 뒤 문자열을 코드 테이블명으로 사용 금지
- 같은 CodeTableName을 가진 여러 Sheet 금지 (충돌)

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

옵션은 `,`로 구분된 토큰 목록이다.

### 토큰 형식

| 형식 | 예시 | 해석 |
|------|------|------|
| `flag` | `pk` | PrimaryKey 지정 (= `pk:true`) |
| `key:value` | `optional:true` | key-value 쌍 |
| `prefix:value` | `gen:ComplexPolicyType` | prefix별 특수 해석 |

**주의:** `key:true`, `key` 옵션은 **미지원**이며 사용 시 빌드가 실패한다.

### 지원 옵션

| 옵션 | 의미 | 비고 |
|------|------|------|
| `pk` | PrimaryKey | 유일한 PK 지정 방식 (flag) |
| `optional:true` | nullable/optional 힌트 | |
| `gen:<EnumName>` | Enum 자동 생성 | TableGen이 해석 |

### PrimaryKey 규칙

- **오직 `pk`만** PrimaryKey로 해석한다.
- `key:true`, `key` 옵션은 빌드 실패한다.
- PrimaryKey는 0개 또는 1개만 허용한다.
- 2개 이상이면 빌드 실패한다.

### gen: 옵션 규칙

`gen:<EnumName>` 옵션 사용 시 다음 조건을 만족해야 한다:

1. **gen 컬럼 = PK 컬럼 (필수)**
   - `gen:<EnumName>`이 선언된 컬럼은 **반드시 `pk`여야 한다**.
   - 즉, gen 컬럼이 곧 PrimaryKey 컬럼이다.
   - gen 컬럼은 테이블당 1개만 허용한다 (2개 이상 FAIL).

2. **gen 컬럼의 타입 (권장)**
   - gen 컬럼의 type은 `enum:<EnumName>`을 권장한다.
   - (예: `gen:ComplexPolicyType` → type은 `enum:ComplexPolicyType`)

3. **EnumName은 유효한 식별자**여야 한다 (`^[A-Za-z_][A-Za-z0-9_]*$`).

4. **Enum 멤버**
   - gen/PK 컬럼의 각 row 값이 enum 멤버 이름이 된다.
   - enum 멤버 값은 **결정적 자동 할당**: 멤버 이름을 오름차순 정렬 후 0부터 순차 할당.

조건 불만족 시 빌드가 실패한다.

### gen: 예시

```
Row 1 (Header):    key                      | fallbackValue | minValue | maxValue
Row 2 (Type):      enum:ComplexPolicyType   | variant       | variant  | variant
Row 3 (Options):   pk, gen:ComplexPolicyType |              |          |
Row 4 (Comment):   (ignored)
Row 5+ (Data):     AttackPower              | i:0           | i:0      | i:10000
                   CriRate                  | f:0           | f:0      | f:3.5
```

여기서:
- `key` 컬럼이 PK이자 gen 컬럼 (같은 컬럼, 옵션: `pk, gen:ComplexPolicyType`)
- `key` 컬럼의 값(`AttackPower`, `CriRate`)이 enum 멤버 이름
- enum 값은 자동 할당: `AttackPower=0`, `CriRate=1` (이름 정렬 순)

상세 규칙은 `skills/devian-tools/11-builder/45-tablegen-enumgen/SKILL.md` 참조.

### Reserved 옵션

- `parser:*` 등 위에 명시되지 않은 키는 Reserved다.
- 존재해도 의미를 부여하지 않는다 (강제/필수 해석 금지).

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

참조 타입 셀 값의 텍스트 규약(DFF)은 `skills/devian-tools/11-builder/31-class-cell-format/SKILL.md`를 따른다.

---

## class: 타입 셀 입력 규칙

`class:` 타입 필드의 셀은 다음 두 가지 형식을 지원한다:

### 입력 형식

| 형식 | 예시 | 동작 |
|------|------|------|
| Raw JSON | `{"save1":123,"save2":456}` | 그대로 NDJSON에 저장 |
| Plain Value | `100` (숫자), `hello` (문자열) | 클래스 전용 파서가 결정적으로 변환 |

Plain Value 입력은 등록된 클래스 파서가 있을 때만 동작하며, 파서가 없으면 Raw JSON 형식만 허용한다.

### Complex 타입 입력 규칙

다음 세 가지 Complex 타입은 빌드 시 평문(Plain Value) 입력을 결정적으로 변환한다.

| 타입 | 평문 예시 | 변환 결과 |
|------|-----------|-----------|
| `class:Devian.CInt` | `100` | `{save1,save2}` object |
| `class:Devian.CFloat` | `1.25` | `{save1,save2}` object |
| `class:Devian.CString` | `hello` | `{data}` object (ComplexUtil 마스킹 후 base64 인코딩) |

빈 셀은 `null`로 처리된다.

### 결정성 규약

Complex 타입의 `save2` 값(마스크)은 다음 규칙에 따라 결정적으로 생성된다:

1. seed 문자열: 시트키, PK값, 컬럼명, 평문값, 타입명을 조합
2. SHA-256 해시의 첫 4바이트를 int32로 사용
3. 동일한 입력은 항상 동일한 출력을 보장 (빌드 diff 안정성)

이 변환은 보안 목적이 아니며, 빌드마다 동일한 결과를 보장하기 위한 것이다.

---

## Outputs

경로 규약은 SSOT를 따른다.

- staging: `{tempDir}/{DomainKey}/cs/Generated/**`, `{tempDir}/{DomainKey}/ts/Generated/**`, `{tempDir}/{DomainKey}/data/ndjson/{TableName}.json` (내용은 NDJSON), `{tempDir}/{DomainKey}/data/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)
- final (각 `{tableDir}` 요소에 대해): `{csConfig.generateDir}/Devian.Domain.{DomainKey}/Generated/**`, `{tsConfig.generateDir}/devian-domain-{domainkey}/Generated/**`, `{tableDir}/ndjson/{TableName}.json` (내용은 NDJSON), `{tableDir}/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)

> **NDJSON 저장 규약:** 파일 확장자는 `.json`이지만, `ndjson/` 폴더의 파일 내용은 NDJSON(라인 단위 JSON)이다. 정본: `skills/devian-tools/11-builder/34-ndjson-storage/SKILL.md`
> **pb64 저장 규약:** 정본: `skills/devian-tools/11-builder/35-pb64-storage/SKILL.md`

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- DFF 규약: `skills/devian-tools/11-builder/31-class-cell-format/SKILL.md`
- Enum Generation: `skills/devian-tools/11-builder/45-tablegen-enumgen/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
