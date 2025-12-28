# Devian – 24 Table Authoring Rules

## Purpose

**XLSX 테이블 작성 규칙을 정의한다. (Strict Mode)**

---

## Belongs To

**Table / Authoring**

---

## 1. 헤더 구조 (4줄 고정)

| Row | 용도 | 설명 |
|-----|------|------|
| 1 | Field Name | 필드 이름 (식별자 규칙) |
| 2 | Type | 타입 |
| 3 | Options | `key:true/false`, `parser:dff`, `optional:true/false` |
| 4 | Comment | Devian은 절대 해석하지 않음 |

**4줄이 아니면 빌드 실패.**

### Header Stop Rule

Row1(필드명)에서 빈 셀을 만나면 그 즉시 헤더 스캔을 중단한다.
- 빈 셀 우측의 모든 Row1~Row4 정보는 무시됨
- 이 규칙은 "스킵"이 아니라 **"중단"**이다

---

## 2. 지원 타입 (Strict Mode)

### Row2 Type Grammar

| 허용 | 금지 |
|------|------|
| `enum:{Name}` / `enum:{Name}[]` | `ref:{Name}` (any casing) |
| `class:{Name}` / `class:{Name}[]` | `ref:Common.{Name}` |
| scalar (int, string, float 등) | `Ref:`, `REF:` 등 |
| scalar array (int[], string[] 등) | |

> **ref: 대신 enum:{X} 또는 class:{Y}를 사용한다**

### Scalar 타입

| 타입 | 설명 | 범위 |
|------|------|------|
| `byte` | 8비트 부호 있는 정수 | -128 ~ 127 |
| `ubyte` | 8비트 부호 없는 정수 | 0 ~ 255 |
| `short` | 16비트 부호 있는 정수 | -32768 ~ 32767 |
| `ushort` | 16비트 부호 없는 정수 | 0 ~ 65535 |
| `int` | 32비트 부호 있는 정수 | |
| `uint` | 32비트 부호 없는 정수 | |
| `long` | 64비트 부호 있는 정수 | |
| `ulong` | 64비트 부호 없는 정수 | |
| `float` | 실수 | |
| `string` | 문자열 | |

> **범위 검증 정책:**
> - `byte`, `ubyte`, `short`, `ushort`의 범위는 **Devian Generator/Loader가 검증**
> - Protobuf는 이 범위를 강제하지 않음
> - 범위 초과 시 **Load 실패** (silent clamp 금지)

### 참조 타입 (enum / class)

| 타입 | 설명 |
|------|------|
| `enum:{Name}` | proto enum 정의 참조 |
| `class:{Name}` | proto message 정의 참조 |
| `enum:Common.{Name}` | Common 도메인 enum 참조 |
| `class:Common.{Name}` | Common 도메인 message 참조 |

> **중요:**
> - `{Name}`은 동일 domain 내에서 유니크한 proto 타입 이름
> - enum은 이름 문자열로 입력
> - class는 DFF object 포맷으로 입력

### 배열 타입

| 타입 | 설명 |
|------|------|
| `byte[]`, `short[]` 등 | Scalar 배열 |
| `string[]` | 문자열 배열 |
| `enum:{Name}[]` | enum 배열 (콤마 구분) |
| `class:{Name}[]` | class 배열 (DFF list of objects) |

---

## 3. Options 행 (Row 3)

### 허용 옵션

| 옵션 | 값 | 설명 |
|------|-----|------|
| `key` | `true` / `false` | Key 컬럼 지정 |
| `parser` | `dff` | 파서 지정 (대소문자 무시, 기본값 dff) |
| `optional` | `true` / `false` | 선택적 필드 |

### Parser 정책 (Strict Mode)

- `parser=dff`가 기본값 (미지정 시 자동 적용)
- parser 키/값은 **대소문자 무시** (`Parser:DFF`, `PARSER:Dff` 모두 허용)
- **dff 이외의 값은 에러** (Strict Mode)

### Optional 기본값 규칙

| 조건 | optional 기본값 |
|------|-----------------|
| `key=true` | `false` |
| `key=false` 또는 미지정 | `true` |

> **key=true && optional=true → 에러** (Key 컬럼은 optional 불가)

### 복합 옵션

콤마로 구분:

```
key:true, optional:false
```

### Key 정책

| 조건 | 결과 |
|------|------|
| `key:true` 없음 | Entity만 생성 |
| `key:true` 1개 | Entity + 컨테이너/로더 생성 |
| `key:true` 2개 이상 | 빌드 실패 (복합키 미지원) |

---

## 4. Data Stop Rule

데이터 행 처리 중 **Key 컬럼 값이 비면** 그 즉시 테이블 로딩을 중단한다.
- 해당 행부터 이후 모든 행은 무시됨
- 이 규칙은 "스킵"이 아니라 **"중단"**이다

---

## 5. 예시

### TestTable.xlsx

| A | B | C | D | E | F |
|---|---|---|---|---|---|
| UserType | number | intArr | stringArr | floatArr | UserProfile |
| enum:UserType | int | int[] | string[] | float[] | class:UserProfile |
| | key:true | | | | |
| (comment) | (comment) | | | | |
| Admin | 1 | -1 | a, b, c | 1.5, 2.2 | id=1; name=Devian; userType=Admin |
| Guest | 2 | 55, 66, 77 | 가, 나, 다 | 7 | |

### 배열 리터럴 예시 (모두 동일 의미)

**Scalar/Enum 배열**:
```
RARE,EPIC,COMMON
{RARE,EPIC,COMMON}
[RARE,EPIC,COMMON]
```

**Class 단일**:
```
id=1; name=Sword
```

**Class 배열**:
```
[id=1; name=A, id=2; name=B]
```

> **주의**: Class에서 `{}` 금지. `{}` 는 scalar/enum 배열 전용.

---

## 6. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | 헤더는 4줄 고정 |
| 2 | Row 1은 Field Name |
| 3 | Row 2는 Type |
| 4 | Row 3은 Options (타입/참조 이름 금지) |
| 5 | Row 4는 Comment (해석 금지) |
| 6 | key:true는 최대 1개 |
| 7 | **ref:{Name} 금지** → enum:{Name} / class:{Name} 사용 |
| 8 | parser=dff 기본값 (대소문자 무시) |
| 9 | key=true → optional 기본값 false |
| 10 | key=true && optional=true → 에러 |
| 11 | Row1 빈 셀에서 헤더 스캔 중단 |
| 12 | Key 값 빈 셀에서 테이블 로딩 중단 |
| 13 | **`{}`는 scalar/enum 배열 전용** (class에서 금지) |

---

## 7. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | 4줄이 아닌 헤더 |
| 2 | 복합키 (key:true 2개 이상) |
| 3 | 허용되지 않은 Options 사용 |
| 4 | Comment 행 해석 |
| 5 | Row 3에 타입/참조 정보 삽입 |
| 7 | parser가 dff 이외의 값 |
| 8 | Key 컬럼에 optional:true |

---

## 8. Table Export Format (NDJSON)

테이블 데이터를 JSON으로 저장할 때는 **NDJSON (JSON Lines)** 포맷을 사용한다.

### 저장 규칙

| 항목 | 규칙 |
|------|------|
| 파일 포맷 | NDJSON (1 row = 1 line) |
| 줄 순서 | Excel 데이터 행 순서 |
| Property 순서 | Excel 컬럼 순서 (Row1 헤더 순서) |
| 알파벳 정렬 | 금지 |

### 예시

```
{"id":1,"name":"Sword","damage":10}
{"id":2,"name":"Shield","damage":0}
```

> **상세**: `28-json-row-io` 스킬 참조

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `27-devian-friendly-format` | DFF 입력 포맷 스펙 |
| `28-json-row-io` | JSON 저장/로드 포맷 (NDJSON) |
| `25-class-cell-format` | class 셀 포맷 |
| `11-core-serializer-protobuf` | proto 매핑 |
| `61-tablegen-implementation` | 테이블 생성 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
## 한 줄 요약

**테이블 헤더 4줄 고정. enum:{Name}/class:{Name} 사용. ref: 금지. parser=dff 기본.**
