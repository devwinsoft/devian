# Devian – 27 Devian Friendly Format (DFF)

## Purpose

Excel/JSON 작성자는 ProtoJSON을 작성하지 않는다. 대신 Devian Friendly Format(DFF)로 입력한다.
Loader/변환기는 Proto Descriptor 기반으로 IMessage를 직접 구성한다.
ProtoJSON은 디버그 출력에만 사용한다.

> **DFF는 Excel authoring(특히 class 셀) 전용이다.**
> JSON 저장/로드 포맷은 `28-json-row-io` 스킬을 참조한다.

---

## Belongs To

**Table / Authoring**

---

## Canonical Policy

- **Input(작성 포맷)**: DFF (Excel authoring)
- **Storage(저장 포맷)**: NDJSON (see `28-json-row-io`)
- **Internal(정본 IR)**: Protobuf IMessage
- **Debug Output**: ProtoJSON (Google.Protobuf.JsonFormatter)

### Parser Policy (Strict Mode)

- `parser=dff`가 기본값. 키/값 모두 **case-insensitive**.
- ProtoJSON authoring은 금지. Excel은 DFF만 사용.
- DFF → DffConverter → DffValue → DffProtobufBuilder → IMessage
- ProtoJSON은 디버그 출력 전용.

---

## DffConverter (정본 규칙)

### 역할

- **DffParser**: 문법만 파싱 (따옴표/이스케이프/구분자)
- **DffConverter**: 타입(Row2 + FieldDescriptor) 기반으로 **허용 문법 강제** 및 **DffValue 정규화**
- **DffProtobufBuilder**: Descriptor 기반 IMessage 직접 구성

### 타입별 허용 문법 (정본)

| 타입 | 허용 문법 | 금지 | 정규화 결과 |
|------|----------|------|-------------|
| **Scalar 단일** | `value` | `{...}`, `[...]`, `a,b,c` | `Scalar("value")` |
| **Scalar[]** | `a,b,c` / `{a,b,c}` / `[a,b,c]` | (모두 허용) | `List([Scalar(...)])` |
| **Enum 단일** | `RARE` | `A,B` / `{A,B}` / `[A,B]` | `Scalar("RARE")` |
| **Enum[]** | `A,B,C` / `{A,B,C}` / `[A,B,C]` | (모두 허용) | `List([Scalar(...)])` |
| **Class 단일** | `k=v; a=b` | `{...}`, `[...]` | `Object({...})` |
| **Class[]** | `[k=v; a=b, k=v; a=b]` | `{...}` | `List([Object(...)])` |

### 배열 리터럴 규칙

**Scalar/Enum 배열**에서는 아래 3가지가 **동일 의미**:
- `a,b,c`
- `{a,b,c}`
- `[a,b,c]`

**Class(Message)에서 `{}` 금지**:
- `{}` 는 scalar/enum 배열 전용 표기
- Class 단일: `k=v; a=b` (pair-list)
- Class 배열: `[k=v; a=b, k=v; a=b]` (bracket only)

---

## 1) Key (필드명) 매칭

허용:
- snake_case
- lowerCamelCase
- PascalCase
- proto field.name / field.jsonName

매칭 순서:
1) field.jsonName exact
2) field.name exact
3) normalize(snake↔camel, case-insensitive)

---

## 2) Value Categories

DFF 문자열은 아래 중 하나로 해석된다.

### 2.1 Empty / Unset
- empty/whitespace → unset
- null/NULL/~ → unset
- "-" 단독 → unset (값으로 쓰려면 "-"를 따옴표로 감싼다)

### 2.2 Scalar
- 기본은 string raw로 보관 후, Descriptor 타입에 의해 변환된다.

### 2.3 List (repeated only)

허용 (모두 동일 의미):
- `a,b,c`
- `[a,b,c]`
- `{a,b,c}` (scalar/enum 배열 전용)

규칙:
- trim
- 빈 항목 제거 (빈 문자열은 ""로 명시)

### 2.4 Object / Map (message or map only)

기본 포맷(pair-list):
- item separator: `;`
- kv separator: `=`

예: `id=1; name=Sword; tags=[rare,event]`

**`{}` 금지** (배열 표기 전용)

---

## 3) Quoting / Escaping

- quote: `"..."` or `'...'`
- escaping (unquoted): `\,` `\;` `\=` `\:` `\[` `\]` `\{` `\}` `\\`

예: `title=Hello\, World`

---

## 4) Type Conversion (Descriptor-driven)

- bool: true/false, t/f, yes/no, y/n, 1/0
- int32/uint32/etc: integer only (no float, no exponent)
- int64/uint64/etc:
  - exponent 금지
  - 2^53-1 초과 값이 숫자 셀로 들어온 경우 기본 에러 (정밀도 손실 방지)
  - 권장: 텍스트 셀로 입력
- float/double: number (NaN/Inf 기본 금지)
- bytes: hex: / b64: prefix required
- enum: name(case-insensitive) 우선, number는 옵션
- message/map/repeated: 재귀 처리
- oneof: 2개 이상 set 시 에러

---

## 5) Well-known Types

- Timestamp: "YYYY-MM-DD", "YYYY-MM-DD HH:MM[:SS]", "YYYY/MM/DD ..." or RFC3339
  - timezone 없는 입력은 Asia/Seoul(+09:00)로 해석
- Duration: "90"(sec), "1h30m", "00:01:30" 등

> **저장(NDJSON) 시 정규화:**
> - Timestamp → RFC3339 UTC string (예: `"2025-12-27T09:00:00Z"`)
> - Duration → protobuf duration string (예: `"1.500s"`, `"60s"`)
> - 상세: `28-json-row-io` 스킬 참조

---

## 6) Error Policy (Default)

- Unknown key: error
- Type conversion fail: error
- Range overflow: error
- oneof conflict: error
- **parser value other than dff: error** (Strict Mode)

Dev mode에서만 allowUnknownFields 등 완화 옵션 허용.

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Excel/JSON 입력은 DFF 포맷만 허용 |
| 2 | ProtoJSON authoring 금지 |
| 3 | Unknown key 발견 시 기본 에러 |
| 4 | 타입 변환 실패 시 에러 |
| 5 | 범위 초과 시 에러 |
| 6 | oneof 충돌(2개 이상 set) 시 에러 |
| 7 | **parser=dff 기본값** (대소문자 무시) |
| 8 | **parser가 dff 이외면 에러** (Strict Mode) |
| 9 | **`{}`는 배열 표기 전용** (class에서 금지) |
| 10 | DffConverter가 타입별 문법을 강제한다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `24-table-authoring-rules` | 테이블 작성 규칙 |
| `25-class-cell-format` | class 셀 포맷 |
| `28-json-row-io` | JSON 저장/로드 포맷 (NDJSON) |
| `11-core-serializer-protobuf` | Protobuf 직렬화 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
## 한 줄 요약

**Excel 입력은 DFF. 저장은 NDJSON. DffConverter가 타입별 문법 강제. IMessage 직접 구성. ProtoJSON은 디버그 출력.**
