# Devian – 25 Class Cell Format

## Purpose

**`class:{Name}` 및 `class:{Name}[]` 타입 셀의 파싱 규칙을 정의한다. (Strict Mode)**

---

## Belongs To

**Table / Codegen**

---

## 1. 적용 범위

| 타입 | 설명 |
|------|------|
| `class:{Name}` | 단일 message 참조 |
| `class:{Name}[]` | message 배열 |
| `class:Common.{Name}` | Common 도메인 message 참조 |

> **enum:{Name}은 24-table-authoring-rules 참조**

---

## 2. 기본 포맷 (DFF)

### class → 단일 message

셀 값은 **DFF object (pair-list)**여야 한다.

```
id=1; name=Alice; userType=Member
```

### class 배열 → message[]

셀 값은 **DFF list of objects**여야 한다.

```
[id=1; name=Alice, id=2; name=Bob]
```

---

## 3. DFF 파싱 규칙

| 항목 | 규칙 |
|------|------|
| 허용 형식 | DFF object / DFF list만 (단일 값 금지) |
| nested object/list | 허용 |
| unknown field | 기본 실패 (오타 방지) |
| enum 필드 값 | 이름 기반만 (`Member`) - 숫자는 옵션 |
| null/unset | `-` 또는 빈 값 |

### DFF Object 포맷

```
key=value; key2=value2
```

- item separator: `;`
- kv separator: `=`
- quoting: `"..."` or `'...'`
- escaping: `\;` `\=` `\\`

### DFF List 포맷

```
[item1, item2, item3]
```

또는

```
item1, item2, item3
```

> 상세 포맷은 `27-devian-friendly-format` 참조

---

## 4. Parser 정책 (Strict Mode)

- 기본 parser는 **dff** (대소문자 무시)
- **parser가 dff 이외의 값이면 에러**
- JSON object/array는 Strict Mode에서 금지

---

## 5. Pluggable Class Parser

### 정책

- class 파싱은 **기본 DFF 파서**를 사용한다
- 프로젝트는 필요시 **ClassParser를 등록해서 대체** 가능
- 대체 파서가 없으면 기본 파서를 **반드시** 사용

### Resolution Order

```
1) 타입별 커스텀 파서 등록됨 → 사용
2) 전역 class 파서 등록됨 → 사용
3) 기본 DFF 파서 → 사용 (항상 존재, fallback)
```

### Interface

```csharp
public interface IClassCellParser
{
    /// <summary>
    /// 셀 값을 파싱하여 인스턴스로 변환
    /// </summary>
    object? Parse(string cellValue, string typeName, bool isArray);
}
```

---

## 6. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | class 셀은 DFF object 포맷이 표준 |
| 2 | 단일 class는 DFF object (pair-list) |
| 3 | class 배열은 DFF list of objects `[...]` |
| 4 | unknown field 발견 시 기본 실패 |
| 5 | enum 필드 값은 이름 문자열 우선 |
| 6 | parser=dff 기본값 (대소문자 무시) |
| 7 | parser가 dff 이외면 에러 (Strict Mode) |
| 8 | **`{}` 금지** (배열 표기 전용, class에서 사용 불가) |

---

## 7. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | 유사 JSON (`{id:1}`) 허용 |
| 2 | ProtoJSON 직접 작성 |
| 4 | parser가 dff 이외 값 |

---

## 8. 예시

### TestTable.xlsx 예시

| UserProfile (class:UserProfile) |
|---------------------------------|
| `id=1; name=Devian; userType=Admin` |
| (빈 셀 = 해당 행에 없음) |

### 배열 예시

| Profiles (class:UserProfile[]) |
|--------------------------------|
| `[id=1; name=A, id=2; name=B]` |

### enum 타입 예시 (24 참조)

| UserType (enum:UserType) |
|--------------------------|
| Admin |
| Guest |

| UserTypes (enum:UserType[]) |
|-----------------------------|
| Admin, Guest, Member |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `27-devian-friendly-format` | DFF 입력 포맷 스펙 |
| `24-table-authoring-rules` | 테이블 작성 규칙, enum 타입 |
| `61-tablegen-implementation` | 테이블 생성 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
## 한 줄 요약

**class 셀은 DFF object, class 배열은 DFF list of objects. parser=dff 기본.**
