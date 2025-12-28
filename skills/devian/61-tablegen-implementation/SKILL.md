# Devian – 61 Tablegen Implementation

## Purpose

**XLSX 테이블을 파싱하여 TableSpec을 생성하고, C#/TypeScript 코드를 생성한다.**

**Phase 3 완료:** `.xlsx` 입력이 지원됨. 워크시트 기반 4행 헤더 파싱.

> JSON은 보조 표현(디버깅/검증/외부 노출)으로만 산출된다.

---

## Domain Root (정본)

**Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다.**

모든 Domain의 실제 루트 경로는 다음으로 고정된다:

```
input/<Domain>/
```

`domains/` 디렉터리는 Devian 구조에 존재하지 않는다.
`{Domain}` 표기는 문서/설명용 플레이스홀더이며,
실제 파일 시스템 경로를 의미하지 않는다.

---

## SSOT 정책

| 구분 | 값 | 설명 |
|------|-----|------|
| **SSOT** | Excel | 스키마의 유일한 진실 원천 |
| **.proto** | 기계 산출물 | Excel에서 생성됨. 사람이 수정하지 않는다 |
| **generated code** | 기계 산출물 | .proto에서 생성됨. 사람이 수정하지 않는다 |

> ⚠️ "proto 직접 작성/관리" 기능은 도입하지 않는다. SSOT는 절대 2개가 되면 안 된다.

### 기계 소유 폴더 (proto-gen/)

각 domain은 기계 생성물 전용 폴더(`proto-gen/`)를 가진다.

```
input/<Domain>/proto-gen/
├── schema/     # .proto 파일 (Excel → 생성)
└── cs/         # C# 코드 (protoc → 생성)
```

### 파이프라인

```
Excel (SSOT)
    ↓
.proto (generated)  → input/<Domain>/proto-gen/schema/
    ↓
protoc
    ↓
C# generated       → input/<Domain>/proto-gen/cs/
```

---

## Belongs To

**Consumer / Tooling (Build tool)**

---

## 공통 핵심 원칙 (최우선)

| # | 원칙 |
|---|------|
| 1 | **Devian은 비즈니스 로직을 다루지 않는다** |
| 2 | **sub-domain / language / region / shard 개념을 해석하지 않는다** |
| 3 | **{table}@{sub} 같은 키의 의미는 개발자 정책이다** |
| 4 | **테이블 종류 1개당 컨테이너는 정확히 1개** |

---

## Sheet / Header / Data 규칙

### Excel 구조

```
ExcelFile 내 각 Sheet = 1 Table
```

### xlsx 파싱 (Phase 3 지원)

**Devian.Tools는 `.xlsx` 파일을 직접 파싱하여 TableSpec을 생성한다.**

```csharp
List<TableSpec> tables = TableSchemaParser.ParseTablesXlsx(xlsxPath, domain);
```

| 항목 | 설명 |
|------|------|
| 입력 | `tablesDir` + `tableFiles` 패턴 (예: `["*.xlsx"]`) |
| 파싱 | 워크시트별로 TableSpec 생성 |
| TableName | 시트명 기반 정규화 (C# 식별자 규칙) |
| 중복 처리 | 동일 이름 시 suffix 추가 (`Foo`, `Foo_2`, `Foo_3`) |

### TableName 정규화 규칙

```
1. 특수문자/공백 → _
2. 연속 _ 정리
3. PascalCase 변환
4. 첫 글자가 숫자면 T prefix
5. 결과가 비면 "Table"
```

예시:
| SheetName | TableName |
|-----------|-----------|
| `Items` | `Items` |
| `Item Stats` | `ItemStats` |
| `123Data` | `T123Data` |
| `@special!` | `Special` |

### Header 규칙 (4줄 고정)

| 행 | 내용 | 비고 |
|----|------|------|
| **Row 1** | Field Name | 필드 이름 (식별자 규칙) |
| **Row 2** | Type | 타입 (prefix 지원) |
| **Row 3** | Options | `key:true/false`, `parser:json`, `optional:true/false` |
| **Row 4** | Comment | 순수 설명용 주석 — **Devian은 절대 해석하지 않음** |
| **Row 5+** | Data | 실제 데이터 |

> **IMPORTANT:**
> - **Row 4(Comment)에 meta/option/policy/constraint 개념은 없다**
> - **tablegen은 Row 4를 로직에 사용하지 않는다**
> - **입력이 4줄 헤더가 아니면 빌드 실패**

---

## Primary Key 규칙

| # | Rule |
|---|------|
| 1 | **`key:true` 옵션으로 지정** (Row 3 Options) |
| 2 | **`key:true`는 최대 1개** (복합키 미지원) |
| 3 | `key:true` 없으면 Entity만 생성, 컨테이너/로더 미생성 |
| 4 | **Key 타입 제한: `ref:*` 금지** (참조 타입은 Key 불가) |
| 5 | **Key 타입 제한: 배열 타입 금지** |
| 6 | 허용: `int`, `uint`, `long`, `ulong`, `string` |

---

## Type 규칙

### Scalar 타입

| 형식 | 설명 | proto 매핑 | 범위 검증 |
|------|------|-----------|----------|
| `byte` | 8비트 부호 있음 | int32 | -128 ~ 127 |
| `ubyte` | 8비트 부호 없음 | uint32 | 0 ~ 255 |
| `short` | 16비트 부호 있음 | int32 | -32768 ~ 32767 |
| `ushort` | 16비트 부호 없음 | uint32 | 0 ~ 65535 |
| `int` | 32비트 부호 있음 | int32 | - |
| `uint` | 32비트 부호 없음 | uint32 | - |
| `long` | 64비트 부호 있음 | int64 | - |
| `ulong` | 64비트 부호 없음 | uint64 | - |
| `float` | 실수 | float | - |
| `string` | 문자열 | string | - |

> **범위 검증 정책 (MUST):**
> - `byte`, `ubyte`, `short`, `ushort`는 proto 타입에 1:1 대응이 없음
> - Protobuf integer 타입으로 매핑되며, **Protobuf는 범위를 강제하지 않음**
> - **범위 검증 책임은 Devian Generator/Loader에 있음**
> - 범위 초과 시 **Load 실패** (silent clamp 금지)

### 참조 타입 (ref)

| 형식 | 설명 |
|------|------|
| `ref:{Name}` | 같은 도메인 내 proto 정의 참조 |
| `ref:Common.{Name}` | Common 도메인 proto 정의 참조 |
| `ref:{Name}[]` | 참조 타입 배열 |

### ref 스캔 범위 (v9)

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

| ref 형식 | 스캔 범위 |
|----------|----------|
| `ref:{Name}` | `<D>/contracts/*.json` |
| `ref:Common.{Name}` | `Common/contracts/*.json` |

### ref 해석 규칙 (MUST)

```
ref:{Name} 또는 ref:Common.{Name}가 등장하면:
1. 스캔 범위 내에서 정의를 찾는다
2. 정의가 0개 → 생성 실패
3. 정의가 2개 이상 (중복) → 생성 실패
```

> **중요:**
> - `{Name}`은 스캔 범위 내에서 **유니크**해야 함
> - `ref:{Name}`이 가리키는 타입은 **반드시 contracts에 정의 존재** 필요
> - Common 외 cross-domain 참조는 **1단계에서 금지**

### 배열 타입

| 형식 | 설명 |
|------|------|
| `byte[]`, `short[]` 등 | Scalar 배열 |
| `ref:{Name}[]` | 참조 타입 배열 |

> 모든 Scalar 타입에 대해 `[]` 배열 허용

---

## Table 생성 규칙

| # | Rule |
|---|------|
| 1 | **Table 1개 = 생성 파일 세트 (C#/TS/JSON)** |
| 2 | **생성된 코드는 ref 대상을 자동 참조** |
| 3 | **field number(tag)는 Tag Registry 기반** |

---

## Tag Registry 정책

**field number(tag)는 Tag Registry로 자동 관리한다.**

### Registry 파일 위치

```
<domain>/proto-gen/manifest/<TableName>.tags.json
```

### Registry 파일 형식

```json
{
  "version": 1,
  "fields": {
    "Id": 1,
    "Name": 2,
    "Cost": 3
  },
  "reserved_tags": [4, 7],
  "reserved_names": ["OldFieldName"]
}
```

### Tag 관리 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | tag는 **단조 증가** 발급 |
| 2 | 발급된 tag는 **절대 변경/재사용 금지** |
| 3 | 삭제 필드는 **reserved**로 남긴다 |
| 4 | rename은 **tag 유지** |

### Add/Rename/Delete 규칙

| 작업 | 규칙 |
|------|------|
| **Add** | 새 필드 등장 → 새 tag 발급 |
| **Rename** | 이름 변경해도 tag 유지, Registry 키 이름만 업데이트 |
| **Delete** | proto에 `reserved <tag>;` 및 `reserved "<name>";` 생성, Registry에도 기록, tag 재사용 금지 |

### reserved 생성 예시

```protobuf
message ItemData {
  uint32 id = 1;
  string name = 2;
  // Cost 필드 삭제됨
  reserved 3;
  reserved "Cost";
}
```

---

## Excel→Proto 검증 (강한 에러 검증)

**생성기는 Excel을 읽고 .proto를 생성하기 전에 아래를 검증한다.**

> **"생성 실패가 정상이다"** — 검증 위반 시 .proto 생성 자체를 실패 처리한다.

### 검증 체크리스트 (MUST)

| # | 검증 항목 | 설명 |
|---|----------|------|
| 1 | **Header 4줄 고정** | Row1/Row2/Row3/Row4 의미 위반 검사 |
| 2 | **Type 문법 (Row2)** | scalar/ref/array만 허용, 포맷 오류 검사 |
| 3 | **Options 문법 (Row3)** | 알 수 없는 옵션 키/값 에러, 타입 정보 삽입 금지 |
| 4 | **ref 대상 존재** | ref:{Name}은 domain 내 정의 필수 |
| 5 | **Data 행 타입/범위 (Row5+)** | 파싱 실패, 범위 초과 에러 |

### Type 문법 검증 (Row2)

허용 타입:
- **scalar**: `byte`, `ubyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `string`
- **ref**: `ref:{Name}`
- **array**: 위 타입들에 `[]`

| 위반 | 처리 |
|------|------|
| 지원하지 않는 타입 | 생성 실패 |
| `ref:` 포맷 오류 | 생성 실패 |
| `[]` 표기 오류 | 생성 실패 |

### Options 문법 검증 (Row3)

| 위반 | 처리 |
|------|------|
| 알 수 없는 옵션 키 | 생성 실패 |
| 값 형식 오류 | 생성 실패 |
| 타입/참조 정보 삽입 | 생성 실패 |

### ref 대상 존재 검증

```
ref:{Name}가 등장하면:
1. proto-gen/schema + 사람 소유 enum proto를 스캔
2. {Name} 정의가 없으면 생성 실패
```

### Data 행 타입/범위 검증 (Row5+)

| 타입 | 검증 |
|------|------|
| 숫자 타입 | 파싱 실패 시 에러 |
| `byte` | -128 ~ 127 |
| `ubyte` | 0 ~ 255 |
| `short` | -32768 ~ 32767 |
| `ushort` | 0 ~ 65535 |
| `uint` / `ulong` | 음수 금지 |
| `ref` (enum) | 유효 값 검증 (1단계 보류 가능) |

> **핵심:** Excel 데이터가 가장 많이 깨지므로, 여기서 최대한 막는다.

---

## Comment 정책 (MUST)

| # | Rule |
|---|------|
| 1 | **4행 Comment는 순수 설명용 주석이다** |
| 2 | **Devian은 4행을 절대 해석하지 않는다** |
| 3 | **데이터 JSON 산출물에 포함하지 않는다** |
| 4 | **meta에 comment 문자열을 저장할 수는 있으나, 로직에 사용하지 않는다** |
| 5 | **4행에 meta/option/policy/constraint 개념은 없다** |

---

## sub-domain / language 정책

| # | Rule |
|---|------|
| 1 | **Devian은 `@`, `_KR`, `_JP` 등을 해석하지 않는다** |
| 2 | 단순 문자열로 취급 |
| 3 | 의미 부여는 개발자 책임 |

---

## 산출물

### Sheet 단위 데이터 JSON

```
{DATA_OUT}/{domain}/{sheetKey}.json
```

### Sheet 단위 meta (67에서 사용)

```
.devian/work/tables/{domain}/{ExcelFileName}.{SheetName}.tablemeta.json
```

### meta 최소 필드

```json
{
  "domain": "Common",
  "excelFileName": "A",
  "sheetName": "Items",
  "tableName": "Items",
  "sheetKey": "items",
  "rowTypeName": "ItemsRow",
  "primaryKeyColumnIndex": 0,
  "primaryKeyFieldName": "id",
  "primaryKeyTypeKey": "int",
  "columns": [
    { "name": "id", "type": "int", "comment": "아이템 ID" },
    { "name": "name", "type": "string", "comment": "이름" }
  ]
}
```

---

## Common 모듈 동기화 (정본)

**Common 도메인 빌드 산출물은 Devian.Common / devian-common 모듈로 복사된다.**

### 동기화 경로

| 언어 | 소스 | 대상 |
|------|------|------|
| C# | Common 도메인 빌드 결과 | `framework/cs/Devian.Common/generated/` |
| TS | Common 도메인 빌드 결과 | `framework/ts/devian-common/generated/` |

### generated / manual 소유권 규칙 (MUST)

| 폴더 | 소유권 | 규칙 |
|------|--------|------|
| `generated/` | 기계 | Common 도메인 빌드 결과만. 사람이 수정 금지. **커밋 필수** |
| `manual/` | 사람 | 개발자 직접 작성. 생성기 **덮어쓰기 금지** |

### 생성 흐름 (Common 기준)

```
1. input/Common → Excel → proto → tag registry → protoc
2. 생성된 C# → framework/cs/Devian.Common/generated
3. 생성된 TS → framework/ts/devian-common/generated
4. 개발자 → manual/에 공용 유틸/helper/facade 작성
5. 외부 도메인 → Devian.Common / devian-common 모듈 의존
```

### Common 모듈 구조

```
framework/cs/Devian.Common/
├── generated/   # 기계 생성, 커밋
└── manual/      # 개발자 작성, 생성기 덮어쓰기 금지

framework/ts/devian-common/
├── generated/   # 기계 생성, 커밋
└── manual/      # 개발자 작성, 생성기 덮어쓰기 금지
```

> **중요:** 도메인(Common)과 모듈(Devian.Common)은 동일하지 않다. 모듈은 도메인의 산출물을 담는 그릇이다.

---

## sheetKey 정규화 규칙

```
function normalizeSheetKey(sheetName):
  result = sheetName.trim()
  result = result.toLowerCase()
  result = result.replace(/[\s\-]+/g, '_')
  return result
```

| SheetName | sheetKey |
|-----------|----------|
| `Items` | `items` |
| `Item Stats` | `item_stats` |

---

## Row 타입 인터페이스 규칙

Row 타입은 `Devian.Core.IEntity`를 구현한다.

PK가 있는 Row는 `Devian.Core.IEntityWithKey<PKType>`를 추가로 구현하며 `GetKey()`를 제공한다.

```csharp
// PK 없는 테이블
public sealed class SomeRow : IEntity { ... }

// PK 있는 테이블
public sealed class ItemsRow : IEntityWithKey<int>
{
    public int Id { get; set; }
    // ...
    
    public int GetKey()
    {
        return Id;
    }
}
```

---

## Table 컨테이너 생성 규칙

각 테이블은 하나의 Table 컨테이너 클래스를 가진다.

Table 컨테이너는 `static class TB_<TableName>` 형태로 생성된다.

```csharp
namespace Devian.Table
{
    public static class TB_<TableName>
    {
        private static readonly IEntityConverter<EntityRow> Converter = ...;
        // ...
    }
}
```

### Converter 구조

Converter는 IR(Protobuf) ↔ Entity **바이너리 변환**을 담당한다.

> **중요: JSON Row I/O는 IEntityConverter를 통하지 않는다.**
>
> JSON Load/Save의 정본 경로는 `28-json-row-io` 스킬에 정의되어 있다:
> - **Load**: general JSON(NDJSON) → Descriptor-driven IMessage build → `entity._LoadProto()`
> - **Save**: `entity._SaveProto()` → proto→general JSON 매핑 규칙 → NDJSON string
>
> `IEntityConverter.ToJson/FromJson`은 **테이블 정식 I/O 경로에서 사용하지 않는다.**

Table Converters consume Protobuf IR via pre-generated artifacts located under each domain's `proto-gen/` directory.

Converter는 테이블별로 하나씩 존재하며, Table 컨테이너 내부에 `private static`으로 선언된다. 외부에 노출되지 않는다.

```csharp
public interface IEntityConverter<TEntity>
{
    // IR 경로 (Protobuf Binary) - 정식 경로
    byte[] ToBinary(IReadOnlyList<TEntity> entities);
    IReadOnlyList<TEntity> FromBinary(byte[] bytes);
    
    // [NOT USED IN TABLE I/O] Legacy/Debug only
    // JSON I/O는 28-json-row-io 정본 파이프라인 사용
    // string ToJson(IReadOnlyList<TEntity> entities);
    // IReadOnlyList<TEntity> FromJson(string json);
}
```

Converter는 컬렉션을 소유하거나 수정하지 않으며, 호출 시점의 고정된 Entity 스냅샷을 입력으로 받기 위해 `IReadOnlyList<TEntity>`를 계약 타입으로 사용한다.

Table(TB)는 `ITableContainer` 인터페이스를 구현하며, binary encode/decode는 `Devian.{Domain}.Bootstrap` 내부 helper가 수행한다.

TB public API에서 converter 타입 노출 없음.

### ITableContainer Implementation

모든 `TB_{TableName}`는 다음 `ITableContainer` 인터페이스를 구현한다:

```csharp
public interface ITableContainer
{
    void Clear();
    void LoadFromJson(string json, LoadMode mode = LoadMode.Merge);
    string SaveToJson();
    void LoadFromBase64(string base64, LoadMode mode = LoadMode.Merge);
    string SaveToBase64();
}
```

### Load API

`LoadFromJson`은 **NDJSON 입력을 정본으로 한다.** 내부 동작은 `28-json-row-io` 파이프라인을 따른다:
- NDJSON 파싱 → Descriptor-driven IMessage build → `entity._LoadProto()` → ApplyLoad
- **IEntityConverter.FromJson은 사용하지 않는다.**

`LoadFromBase64`는 내부적으로 `Bootstrap._Decode_{TableName}(bytes)`를 호출한다.

LoadMode의 해석 및 적용은 Table 컨테이너가 담당한다.

```csharp
public void LoadFromJson(string json, LoadMode mode = LoadMode.Merge);     // 정본: IMessage build + _LoadProto
public void LoadFromBase64(string base64, LoadMode mode = LoadMode.Merge); // Bootstrap._Decode 호출
```

### Save API

`SaveToJson`은 **NDJSON string을 반환한다.** 내부 동작은 `28-json-row-io` 파이프라인을 따른다:
- `entity._SaveProto()` → proto→general JSON 매핑 규칙 → NDJSON string
- **IEntityConverter.ToJson은 사용하지 않는다.**

`SaveToBase64`는 내부적으로 `Bootstrap._Encode_{TableName}(rows)`를 호출한다.

```csharp
public string SaveToJson();   // 정본: _SaveProto + 28 매핑 규칙
public string SaveToBase64(); // Bootstrap._Encode 호출
```

### Clear API

`Clear()`는 캐시된 Row 인스턴스를 제거한다. 기존 `Unload()` 대신 사용.

```csharp
public void Clear();
```

### LoadMode enum

LoadMode는 데이터 병합/교체 동작을 제어한다.

```csharp
namespace Devian.Core
{
    public enum LoadMode
    {
        Merge,      // 기존 캐시 유지 + 새 데이터 병합. key 충돌 시 overwrite
        Replace     // 기존 캐시 Clear 후 로드
    }
}
```

### Bootstrap (Domain-level)

`Devian.{Domain}.Bootstrap` 클래스가 converter singleton과 binary encode/decode helper를 관리한다.

```csharp
// Bootstrap.g.cs (generated)
public static class Bootstrap
{
    public static void Init();  // converter 초기화 (idempotent)
    
    internal static IReadOnlyList<RowType> _Decode_{TableName}(byte[] bytes);
    internal static byte[] _Encode_{TableName}(IReadOnlyList<RowType> rows);
}
```

- `Init()`: 중복 호출 안전 (idempotent)
- `_Decode/_Encode`: TB가 Base64 I/O 시 내부적으로 호출
- converter 타입은 Bootstrap 내부에만 존재

---

## Generated Code Style

| # | Rule |
|---|------|
| 1 | **블록 네임스페이스 사용** — `namespace X { }` 형태 (파일 범위 `namespace X;` 금지) |
| 2 | **C# 9 호환** — netstandard2.1 타겟, C# 10+ 전용 문법 금지 |
| 3 | **auto-generated 헤더** — 모든 .g.cs 파일 상단에 `// <auto-generated>` 주석 |

이 규칙은 생성기(CSharpTableGenerator, CSharpBootstrapGenerator 등)와 생성물(.g.cs) 모두에 적용된다.

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | ExcelFile 내 각 Sheet = Table로 처리 |
| 2 | Header 4행 고정, 데이터 5행부터 |
| 3 | Primary Key는 `key:true` 옵션으로 지정 (최대 1개) |
| 4 | **Table 1개 = proto 파일 1개** |
| 5 | **field number는 Tag Registry 기반** |
| 6 | **ref:{Name}이 가리키는 proto 정의 없으면 빌드 실패** |
| 7 | **Excel→Proto 검증 위반 시 생성 실패** |
| 8 | Comment(Row 4)는 JSON 미포함 |
| 9 | **sub-domain 해석 안 함** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `24-table-authoring-rules` | 테이블 작성 규칙 |
| `11-core-serializer-protobuf` | proto 매핑 |
| `28-json-row-io` | JSON I/O 정본 파이프라인 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.1.0 | 2025-12-28 | Phase 3: xlsx 파싱 지원 |
| 1.0.0 | 2025-12-28 | Initial |
