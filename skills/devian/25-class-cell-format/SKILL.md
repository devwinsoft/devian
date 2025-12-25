# Devian – 25 Class Cell Format

## Purpose

**`class:{ClassName}` 및 `class:{ClassName}[]` 타입 셀의 파싱 규칙을 정의한다.**

---

## Belongs To

**Table / Codegen**

---

## 1. 적용 범위

| 타입 | 설명 |
|------|------|
| `class:{ClassName}` | 단일 클래스 객체 |
| `class:{ClassName}[]` | 클래스 객체 배열 |

그 외 타입(`string`, `int`, `enum:*`, `*[]` 등)은 기존 primitive/enum/array 규칙 적용.

---

## 2. 기본 포맷: JSON만 허용

### class 단일

셀 값은 **JSON object**여야 한다.

```json
{"id":1,"name":"Alice","userType":"Member"}
```

### class 배열

셀 값은 **JSON array of objects**여야 한다.

```json
[{"id":1,"name":"Alice","userType":"Member"},{"id":2,"name":"Bob","userType":"Guest"}]
```

### 금지된 포맷

| 포맷 | 예시 | 이유 |
|------|------|------|
| 유사 JSON | `{id:1}` | 따옴표 없음 |
| key-value DSL | `id=1,name=Alice` | 비표준 |
| 콤마 리스트 | `Alice, Bob` | 모호함 |

---

## 3. JSON 파싱 규칙

| 항목 | 규칙 |
|------|------|
| 허용 형식 | object / array만 (단일 값 금지) |
| nested object/array | 허용 |
| unknown field | 기본 실패 (오타 방지) |
| enum 값 | 이름 기반만 (`"Member"`) - 숫자 금지 |
| null | 기본 금지 (nullable 정책 미도입) |

### unknown field 옵션

```csharp
// 기본: false (unknown field 발견 시 실패)
// 필요시 옵션으로 변경 가능
allowUnknownFields: false
```

---

## 4. Pluggable Class Parser

### 정책

- class 파싱은 **기본 JSON 파서**를 사용한다
- 프로젝트는 필요시 **ClassParser를 등록해서 대체** 가능
- 대체 파서가 없으면 JSON 파서를 **반드시** 사용

### Resolution Order

```
1) 클래스별 커스텀 파서 등록됨 → 사용
2) 전역 class 파서 등록됨 → 사용
3) 기본 JSON class 파서 → 사용 (항상 존재, fallback)
```

### Interface

```csharp
public interface IClassCellParser
{
    /// <summary>
    /// 셀 값을 파싱하여 클래스 인스턴스로 변환
    /// </summary>
    object? Parse(string cellValue, string className, bool isArray);
}
```

---

## 5. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | class 셀은 JSON 포맷만 허용 |
| 2 | 단일 class는 JSON object |
| 3 | class 배열은 JSON array of objects |
| 4 | unknown field 발견 시 기본 실패 |
| 5 | enum 값은 이름 문자열만 허용 |
| 6 | null 값은 기본 금지 |

---

## 6. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | 유사 JSON (`{id:1}`) 허용 |
| 2 | key-value DSL 포맷 |
| 3 | 콤마 구분 리스트로 class 배열 표현 |
| 4 | enum 값에 숫자 사용 |

---

## 7. 예시

### TestTable.xlsx 예시

| UserProfile (class:UserProfile) |
|---------------------------------|
| `{"id":1,"name":"Devian","userType":"Admin"}` |
| (빈 셀 = 해당 행에 없음) |

### 배열 예시

| Profiles (class:UserProfile[]) |
|--------------------------------|
| `[{"id":1,"name":"A"},{"id":2,"name":"B"}]` |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `24-table-authoring-rules` | 테이블 작성 규칙 |
| `61-tablegen-implementation` | 테이블 생성 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0 | 2024-12-25 | Initial - Class cell format policy |

---

## 한 줄 요약

**class 셀은 JSON만 허용하며, 파서는 교체 가능하지만 기본은 항상 JSON이다.**
