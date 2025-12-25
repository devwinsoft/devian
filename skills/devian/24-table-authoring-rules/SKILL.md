# Devian – 24 Table Authoring Rules

## Purpose

**XLSX 테이블 작성 규칙을 정의한다.**

---

## Belongs To

**Table / Authoring**

---

## 1. 헤더 구조 (4줄 고정)

| Row | 용도 | 설명 |
|-----|------|------|
| 1 | Field Name | 필드 이름 (식별자 규칙) |
| 2 | Type | 타입 |
| 3 | Options | `key:true/false`, `parser:json`, `optional:true/false` |
| 4 | Comment | Devian은 절대 해석하지 않음 |

**4줄이 아니면 빌드 실패.**

---

## 2. 지원 타입

### 기본 타입

| 타입 | 설명 |
|------|------|
| `string` | 문자열 |
| `int` | 정수 |
| `float` | 실수 |
| `bool` | 불리언 |
| `json` | 임의 JSON |

### 배열 타입

| 타입 | 설명 |
|------|------|
| `string[]` | 문자열 배열 |
| `int[]` | 정수 배열 |
| `float[]` | 실수 배열 |
| `bool[]` | 불리언 배열 |

### 확장 타입

| 타입 | 설명 |
|------|------|
| `enum:Name` | 열거형 (contracts에 정의) |
| `enum:Name[]` | 열거형 배열 |
| `class:Name` | 클래스 (contracts에 정의) |
| `class:Name[]` | 클래스 배열 |

---

## 3. Options 행 (Row 3)

### 허용 옵션

| 옵션 | 값 | 설명 |
|------|-----|------|
| `key` | `true` / `false` | Key 컬럼 지정 |
| `parser` | `json` | 파서 지정 |
| `optional` | `true` / `false` | 선택적 필드 |

### 복합 옵션

콤마로 구분:

```
key:true, optional:true
```

### Key 정책

| 조건 | 결과 |
|------|------|
| `key:true` 없음 | Entity만 생성 |
| `key:true` 1개 | Entity + 컨테이너/로더 생성 |
| `key:true` 2개 이상 | 빌드 실패 (복합키 미지원) |

---

## 4. 예시

### TestTable.xlsx

| A | B | C | D | E | F |
|---|---|---|---|---|---|
| UserType | number | intArr | stringArr | floatArr | UserProfile |
| enum:UserType | int | int[] | string[] | float[] | class:UserProfile |
| | key:true | | | | |
| (comment) | (comment) | | | | |
| Admin | 1 | -1 | a, b, c | 1.5, 2.2 | {"id":1, ...} |
| Guest | 2 | 55, 66, 77 | 가, 나, 다 | 7 | |

---

## 5. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | 헤더는 4줄 고정 |
| 2 | Row 1은 Field Name |
| 3 | Row 2는 Type |
| 4 | Row 3은 Options |
| 5 | Row 4는 Comment (해석 금지) |
| 6 | key:true는 최대 1개 |

---

## 6. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | 3줄 또는 5줄 이상의 헤더 |
| 2 | 복합키 (key:true 2개 이상) |
| 3 | 허용되지 않은 Options 사용 |
| 4 | Comment 행 해석 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `25-class-cell-format` | class 셀 포맷 |
| `61-tablegen-implementation` | 테이블 생성 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.2.0 | 2024-12-25 | 4줄 헤더로 변경, Options 행 추가 |
| 0.1.0 | 2024-12-20 | Initial |

---

## 한 줄 요약

**테이블 헤더는 4줄 고정: Name, Type, Options, Comment.**
