# Devian – 28 JSON Row I/O (NDJSON)

## Purpose

- Save/Load format for table rows is **general JSON**.
- The canonical storage format is **NDJSON (JSON Lines)**: 1 row = 1 JSON line.
- Loading must normalize row JSON into **Protobuf IMessage** and then call `_LoadProto()`.

### JSON I/O 정본 경로 (SSOT)

> **JSON Load 정본:**
> `general JSON(NDJSON) → Descriptor-driven IMessage build → entity._LoadProto()`
>
> **JSON Save 정본:**
> `entity._SaveProto() → (proto→general JSON 매핑 규칙) → NDJSON string`
>
> **IEntityConverter.FromJson/ToJson은 테이블 정식 I/O 경로에서 사용하지 않는다.**

---

## Belongs To

**Table / Authoring**

---

## Canonical Format (NDJSON)

- Each line is a JSON object representing one row.
- Line order MUST match Excel data row order.
- Object property order MUST match Excel column order (Row1 header order).
- Do NOT sort keys alphabetically.

### Example

```
{"id":1,"name":"Sword","damage":10}
{"id":2,"name":"Shield","damage":0}
{"id":3,"name":"Bow","damage":7}
```

---

## Load Pipeline

```
general JSON (row object)
    ↓
Descriptor-driven IMessage build
    ↓
entity._LoadProto(proto)
```

**`_LoadJson(protoJson)` exists for debug/test only and MUST NOT be used by the table loading pipeline.**

### Load Steps

1. Read one line from NDJSON (or one element from JSON array)
2. Parse as JSON object
3. Build IMessage using `DffProtobufBuilder` (Descriptor-driven)
4. Call `entity._LoadProto(proto)`

---

## Save Pipeline

### Save Unit (SSOT)

> **Table Container Naming:** `TB_{TableName}` is the canonical table container type name. `T_{TableName}` is invalid and must not appear in generated code or docs.

- **모든 `TB_{TableName}`는 `ITableContainer` 인터페이스를 구현한다.**
- **Save 단위는 단일 테이블 컨테이너 (`TB_{TableName}`)**
- 집계 저장 엔트리포인트(`SaveAll()`, `SaveTables()` 등)는 제공하지 않는다.

### ITableContainer API (정본)

| 메서드 | 설명 |
|--------|------|
| `Clear()` | 캐시된 Row 인스턴스 제거 |
| `LoadFromJson(string json, LoadMode mode)` | NDJSON string에서 로드 |
| `SaveToJson()` | NDJSON string으로 반환 |
| `LoadFromBase64(string base64, LoadMode mode)` | Base64(delimited binary)에서 로드 |
| `SaveToBase64()` | Base64(delimited binary)로 반환 |

> - `json`은 **NDJSON string** (1 row = 1 line). Root array 금지.
> - Base64는 내부적으로 **delimited binary + base64 wrapper**.
> - Binary encode/decode는 `Devian.{Domain}.Bootstrap` 내부 helper가 수행.
> - TB public API에서 converter 타입 노출 없음.

### Save Flow

```
Table Container (TB_{TableName})
    ↓
TB_{TableName}.SaveToJson() → NDJSON string
    ↓
property order follows Excel column order
line order follows container's deterministic row order
```

### Save Steps

1. Container의 캐시된 Row 인스턴스들을 순회
2. 각 Row를 JSON object로 변환 (property 순서 = Excel 컬럼 순서)
3. 한 줄에 하나의 JSON object (NDJSON)
4. 전체 NDJSON을 string으로 반환

### Line Order Policy

- 줄 순서는 컨테이너가 제공하는 **결정적(deterministic) row 순서**를 따른다.
- 가능하면 **로드 순서(load order)**를 유지한다.
- 정렬이 필요한 경우 컨테이너 구현에서 결정한다.

### Save Implementation

- Save는 `row._SaveProto()` 결과(IMessage)를 기반으로 일반 JSON을 생성한다.
- `_LoadJson(protoJson)` 경로를 사용하지 않는다.
- 변환 규칙은 아래 "Proto → General JSON Value Mapping" 섹션을 따른다.

---

## Proto → General JSON Value Mapping (Canonical)

Save 시 Protobuf IMessage(또는 field value)를 일반 JSON 값으로 변환하는 정본 규칙.

### Common Rules

- 입력: Protobuf IMessage 또는 field value
- 출력: 일반 JSON 값 (number/string/bool/null/object/array)
- **Unset 필드는 omit** (키를 쓰지 않음)

### Scalar Types

| Proto Type | JSON Type | Notes |
|------------|-----------|-------|
| `bool` | boolean | `true` / `false` |
| `string` | string | |
| `float`, `double` | number | **NaN, Infinity → error (Strict)** |
| `int32`, `sint32`, `sfixed32` | number | |
| `uint32`, `fixed32` | number | |
| `int64`, `sint64`, `sfixed64` | **string** | 정밀도 보존 (예: `"9223372036854775807"`) |
| `uint64`, `fixed64` | **string** | 정밀도 보존 |
| `bytes` | string | base64 encoded |

> **64-bit 정수는 JSON string으로 저장한다.** JS/툴 체인에서 JSON number의 정밀도 손실(2^53-1 초과)을 방지하기 위함.

### Enum

- **enum name 문자열**로 저장 (예: `"EPIC"`, `"RARE"`)
- **숫자값 저장 금지 (Strict)**

### Repeated

- `repeated scalar/enum/message` → JSON array
- 원소 변환은 동일 규칙 재귀 적용

### Map

- `map<K, V>` → JSON object
- key는 string으로 변환:
  - `string` key: 그대로
  - 숫자/불리언 key: `ToString()` 결과
- value는 동일 규칙 재귀 적용

### Message (Nested)

- `message` → JSON object
- **필드 순서: field number ascending** (결정성 보장)
- unset 필드는 omit
- **oneof: set된 필드만 출력** (2개 이상 set 시 에러)

### Well-known Types

| Type | JSON | Example |
|------|------|---------|
| `google.protobuf.Timestamp` | string (RFC3339 UTC) | `"2025-12-27T09:00:00Z"` |
| `google.protobuf.Duration` | string (protobuf duration) | `"1.500s"`, `"60s"` |
| `google.protobuf.*Value` (wrappers) | scalar value | wrapper의 value만 출력 |

---

## Determinism (Save)

Save 출력의 결정성(deterministic)을 보장하기 위한 필드 순서 규칙.

| Level | Property Order |
|-------|----------------|
| **Top-level row** | Excel column order (Row1 header) |
| **Nested message** | field number ascending |
| **Map entries** | key ascending (lexicographical for string, numerical for int) |

---

## Strict Error Policy (Save)

| Error | Behavior |
|-------|----------|
| NaN / Infinity | error |
| Enum numeric value | error |
| oneof conflict (2+ set) | error |
| Unknown field in proto | error |

---

## Null vs Missing

- **Missing property** means "unset" (field not present in source).
- **`null`** is allowed only if the field type allows it by policy.
- Default is to treat `null` as error (Strict) unless explicitly allowed.

| Input | Meaning | Default Behavior |
|-------|---------|------------------|
| property missing | unset | allowed |
| `"field": null` | explicit null | error (Strict) |
| `"field": ""` | empty string | allowed for string type |

---

## Error Policy (Strict default)

| Error | Behavior |
|-------|----------|
| Unknown key | error |
| Type mismatch | error |
| oneof conflict | error |
| null on non-nullable | error |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Storage format is NDJSON (1 row = 1 line). Root array forbidden. |
| 2 | Property order = Excel column order (Row1 header) |
| 3 | Line order = container's deterministic row order (prefer load order) |
| 4 | Load path: JSON → IMessage → `_LoadProto()` |
| 5 | `_LoadJson()` is debug/test only |
| 6 | Unknown key → error (Strict) |
| 7 | Type mismatch → error |
| 8 | **`TB_{TableName}` implements `ITableContainer`** |
| 9 | **API: `Clear()`, `LoadFromJson()`, `SaveToJson()`, `LoadFromBase64()`, `SaveToBase64()`** |
| 10 | **Aggregated save APIs (`SaveAll()`, `SaveTables()`) are forbidden** |
| 11 | **Save uses `_SaveProto()` + mapping rules (not `_LoadJson()`)** |
| 12 | **64-bit integers → JSON string** |
| 13 | **Enum → name string (numeric forbidden)** |
| 14 | **Nested message field order = field number ascending** |
| 15 | **Timestamp → RFC3339 UTC string** |
| 16 | **Duration → protobuf duration string (e.g. "1.500s")** |
| 17 | **NaN/Infinity → error (Strict)** |
| 18 | **Binary encode/decode is handled by `Devian.{Domain}.Bootstrap` internal helpers** |
| 19 | **TB public API does not expose converter types** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `24-table-authoring-rules` | Excel 테이블 작성 규칙 |
| `27-devian-friendly-format` | Excel authoring용 DFF 포맷 |
| `11-core-serializer-protobuf` | Protobuf 직렬화 정책 |
| `61-tablegen-implementation` | tablegen 구현 |
| `02-skill-specification` | Skill 규격 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
## 한 줄 요약

**테이블은 ITableContainer 구현. JSON은 NDJSON string. Base64는 Bootstrap 내부 helper. converter 노출 금지.**
