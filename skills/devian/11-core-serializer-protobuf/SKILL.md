# Devian – 11 Core Serializer: Protobuf

## Purpose

**이 Skill은 Protobuf 기반 IR 처리를 제공한다.**

IR의 유일한 canonical representation은 Protobuf이다. Devian.Core는 이 모듈에 직접 의존하지 않는다.

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

## SSOT 정책 (Single Source of Truth)

| 구분 | 값 | 설명 |
|------|-----|------|
| **SSOT** | Excel | 스키마의 유일한 진실 원천 |
| **.proto** | 기계 산출물 | Excel에서 생성됨. 사람이 수정하지 않는다 |
| **protoc 출력** | 기계 산출물 | .proto에서 생성됨. 사람이 수정하지 않는다 |

### 보류 선언

"proto 직접 작성/관리" 기능은 이번 단계에서 도입하지 않는다. SSOT는 절대 2개가 되면 안 된다.

---

## 파이프라인 개요

```
Excel (SSOT)
    ↓
.proto (generated)  → input/<Domain>/proto-gen/schema/
    ↓
protoc
    ↓
C# generated       → input/<Domain>/proto-gen/cs/
```

Generated .proto and generated language bindings are stored under each domain's `proto-gen/` folder.

### proto-gen 커밋/빌드 정책

This module assumes `proto-gen/` artifacts are already generated and committed. It does not invoke protoc at runtime or build time.

| 정책 | 값 |
|------|-----|
| proto-gen 커밋 | ✅ committed to repository |
| 빌드 시 protoc 실행 | ❌ not required |
| 런타임 시 protoc 실행 | ❌ not required |
| 생성 시점 | 명시적 도구 실행 시에만 |

> Proto generation is an explicit developer action, not a build step.

---

## Belongs To

**Core Runtime**

> Serializer는 Core의 **확장 옵션(Skill)**이지,  
> contracts나 도메인 규칙이 아니다.

---

## Entity Serialization Contract (정본)

Devian에서 엔티티 직렬화는 protobuf 기반 파이프라인을 사용한다.

### Entity Internal Methods

모든 도메인 엔티티는 아래 내부 전용 메소드를 가진다.

- `_LoadProto(TProto msg)`
- `_SaveProto(): TProto`
- `_LoadJson(string json)`
- `_SaveJson(): string`

`_LoadProto / _SaveProto`는 필드 단위 encode/decode(암호화/복호화 포함)를 담당한다.

`_LoadJson / _SaveJson`은 **디버그/검증/도구 출력용** protobuf JSON 포맷을 사용하며,
`Google.Protobuf.JsonParser / JsonFormatter`를 통해
반드시 `_LoadProto / _SaveProto`로 위임한다.

> **주의:** Excel 입력은 ProtoJSON이 아니라 DFF(Devian Friendly Format)이다.

### 생성 코드 책임 (MUST)

| # | 규칙 |
|---|------|
| 1 | `_LoadProto/_SaveProto/_LoadJson/_SaveJson`은 **생성 코드(generated)**의 책임이다 |
| 2 | 엔티티는 **partial class**로 생성하여 manual 코드와 분리한다 |
| 3 | generated 코드는 **수정 금지** |
| 4 | manual 코드는 생성기가 **덮어쓰지 않는다** |

### JSON 포맷 규칙 (MUST) — ProtoJSON 전용

> **주의:** 이 규칙은 **ProtoJSON(디버그/검증/도구 출력용)**에 대한 규칙이다.
> **테이블 정식 JSON I/O는 `28-json-row-io`이며, `IEntityConverter.ToJson/FromJson`은 사용하지 않는다.**

| # | 규칙 |
|---|------|
| 1 | ProtoJSON은 **디버그/검증/도구 출력용**이며, 입력 작성 포맷이 아니다 |
| 2 | JSON 직렬화/역직렬화는 **반드시 Google.Protobuf.JsonFormatter / JsonParser**를 사용한다 |
| 3 | System.Text.Json, Newtonsoft.Json 등 다른 JSON serializer 사용 **금지** |

> **Excel 입력은 DFF(Devian Friendly Format)**이며, Loader가 Proto Descriptor 기반으로 IMessage를 구성한다.
> 상세: `27-devian-friendly-format`

### Converter Call Flow (정본) — Binary/ProtoJSON 경로

Converter는 `IEntityConverter<TEntity>` 계약을 구현하며, 아래 4가지 흐름으로 동작한다.

> **주의:** `FromJson/ToJson`은 **ProtoJSON 디버그 전용 경로**이다.
> **테이블 정식 JSON I/O는 `28-json-row-io` 파이프라인을 사용한다.**

| 흐름 | 동작 |
|------|------|
| `FromBinary(bytes)` | `MessageParser<TProto>.ParseFrom(bytes)` → `entity._LoadProto(proto)` |
| `ToBinary(entity)` | `entity._SaveProto()` → `proto.ToByteArray()` |
| `FromJson(json)` | `JsonParser.Parse<TProto>(json)` → `entity._LoadProto(proto)` **(ProtoJSON 디버그용)** |
| `ToJson(entity)` | `entity._SaveProto()` → `JsonFormatter.Format(proto)` **(ProtoJSON 디버그용)** |

Converter는 엔티티의 `_LoadProto/_SaveProto/_LoadJson/_SaveJson`만 호출한다.
JSON 처리는 `Google.Protobuf.JsonParser / JsonFormatter`만 사용한다.

> 상세: [docs/serialization/converter-call-flow.md](../../docs/serialization/converter-call-flow.md)

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Protobuf 기반 IR 직렬화/역직렬화 | `IEntityConverter<TEntity>` 구현 |
| Excel → .proto 생성 | Devian.Tools에서 수행 |
| Core extension으로서의 serializer | 선택적 확장 모듈 |

### Out of Scope (Skill 영역)

| 항목 | 담당 Skill |
|------|-----------|
| table schema 정의 | contracts 영역 |
| contracts 구조 | 가정하지 않음 |
| server transport | Server Skill |
| application lifecycle | 각 플랫폼 Skill |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | **SSOT = Excel**. proto가 SSOT처럼 읽히면 안 된다 |
| 2 | 생성된 .proto / generated code는 **사람이 수정 금지** |
| 3 | `Devian.Core`는 Protobuf를 **직접 참조하지 않는다** |
| 4 | `IProtoEntity<TProto>`는 **`Devian.Protobuf`에 정의**된다 (Core 아님) |
| 5 | IR의 유일한 canonical representation은 **Protobuf**이다 |
| 6 | Serializer는 **optional component**이다 |
| 7 | **Excel authoring은 DFF**이며, **ProtoJSON authoring은 금지** |
| 8 | **`_LoadJson()` / `_SaveJson()`은 디버그/테스트 전용** |
| 9 | **Table loading pipeline에서 `_LoadJson()` 호출 금지** |

### Canonical Load Pipeline

```
general input (Excel/JSON)
    ↓
Descriptor-driven IMessage build
    ↓
entity._LoadProto(proto)
```

> **`_LoadJson(protoJson)`은 디버그/테스트용으로만 남겨둔다.**
> Table loading pipeline (Excel/JSON 로드)에서는 반드시 IMessage → `_LoadProto()` 경로를 사용한다.

---

## Excel → Proto 타입 매핑

### Scalar 매핑

| Excel 타입 | Proto 타입 | 범위 검증 |
|-----------|-----------|----------|
| `byte` | `int32` | -128 ~ 127 |
| `ubyte` | `uint32` | 0 ~ 255 |
| `short` | `int32` | -32768 ~ 32767 |
| `ushort` | `uint32` | 0 ~ 65535 |
| `int` | `int32` | - |
| `uint` | `uint32` | - |
| `long` | `int64` | - |
| `ulong` | `uint64` | - |
| `float` | `float` | - |
| `string` | `string` | - |

> **Scalar types `byte`, `ubyte`, `short`, and `ushort` are mapped to protobuf integer types.
> Their semantic value ranges are enforced by Devian loaders and converters,
> not by protobuf itself.**

### 범위 검증 정책 (MUST)

| # | Rule |
|---|------|
| 1 | Protobuf에는 byte/short 전용 타입이 없음 |
| 2 | 위 매핑은 wire-level 호환을 위한 결정 |
| 3 | **범위 검증 책임은 Devian Generator/Loader에 있음** |
| 4 | 범위 초과 시 **Load 실패** |
| 5 | **silent clamp 금지** |
| 6 | **암묵적 변환 금지** |

### ref 매핑

| Excel 타입 | Proto 타입 |
|-----------|-----------|
| `ref:{Name}` | `{Name}` (local import 자동 생성) |
| `ref:Common.{Name}` | `{Name}` (Common import 자동 생성) |
| `ref:{Name}[]` | `repeated {Name}` |

### ref 스캔 범위 (v9)

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

| ref 형식 | 스캔 범위 |
|----------|----------|
| `ref:{Name}` | `<D>/contracts/*.json` |
| `ref:Common.{Name}` | `Common/contracts/*.json` |

> **중요:**
> - ref 해석 로직의 책임은 **생성기**에 있음
> - 생성기는 스캔 범위 내의 contracts를 스캔하여 타입 판단
> - Common 외 cross-domain 참조는 **1단계에서 금지**

### 배열 매핑

| Excel 타입 | Proto 타입 |
|-----------|-----------|
| `byte[]` | `repeated int32` |
| `short[]` | `repeated int32` |
| `int[]` | `repeated int32` |
| `string[]` | `repeated string` |
| `ref:{Name}[]` | `repeated {Name}` |

> 모든 Scalar 타입에 대해 `[]` 배열 허용

### 1단계 금지 (미지원)

| 타입 | 상태 |
|------|------|
| `map<>` | 금지 |
| `oneof` | 금지 |
| custom options | 금지 |
| nested messages | 금지 (2단계 검토) |

---

## 플랫폼별 주의 사항

### TypeScript

| 이슈 | 설명 |
|------|------|
| `int64` / `uint64` | JS number로 정확히 표현 불가 |
| TS codegen | 1단계에서 보류 |
| 향후 전략 | bigint / string 변환 검토 필요 |

### C# / Unity

모든 scalar 매핑 안전. 별도 주의 사항 없음.

---

## Table ↔ Proto 대응 규칙

| # | Rule |
|---|------|
| 1 | **Table 1개 = proto 파일 1개** |
| 2 | **proto 파일은 message 1개만 포함** |
| 3 | **enum은 사람이 작성한 proto에 존재** |
| 4 | **생성된 proto는 ref 대상을 import** |

### import 생성 규칙

```
ref:{Name}가 등장하면:
1. 동일 proto에 정의되어 있지 않으므로 항상 import 대상
2. import 대상 proto 파일은 {name}.proto 규칙으로 매핑
3. 생성기가 proto-gen/schema 내 proto 파일을 스캔하여 {Name}이 enum인지 message인지 판단
```

---

## Field Number 정책

**필드 번호는 고정이어야 한다.** 컬럼 순서 변경이 wire format 변경을 일으키면 안 된다.

### 구현 방식

Excel/tables.json에 `fieldId` 컬럼을 도입하여 필드 번호를 명시적으로 고정한다.

```json
{
  "columns": [
    { "name": "id", "type": "int", "fieldId": 1 },
    { "name": "name", "type": "string", "fieldId": 2 }
  ]
}
```

`fieldId`가 0이면 순서 기반 자동 할당 (권장하지 않음).

---

## Directory & Project Constraints

### 도메인별 기계 소유 폴더 (proto-gen/)

각 domain은 `proto-gen/` 폴더를 가진다. 이 폴더는 기계 생성물 전용이며 사람이 수정하지 않는다.

```
devian/
├── input/
│   └── <Domain>/
│       └── proto-gen/           ← 기계 소유 폴더 (수정 금지)
│           ├── schema/          ← .proto 파일 (Excel → 생성)
│           │   └── <domain>.proto
│           └── cs/              ← C# 코드 (protoc → 생성)
│               └── *.cs
```

### Project References

- `Devian.Protobuf` → `Devian.Core` ✅
- `Devian.Core` → `Devian.Protobuf` ❌

> **구조 변경 (2025)**: `Devian.ProtoGen`은 제거되었습니다.
> 런타임 Protobuf 변환기(`ProtobufEntityConverter`)는 `Devian.Protobuf`에,
> 코드 생성기는 `Devian.Tools`에 있습니다.

### Allowed Dependencies

- `Google.Protobuf` NuGet 패키지

### Forbidden Dependencies

- UnityEngine.*
- 서버 전용 런타임

---

## Responsibilities

1. **`IEntityConverter<TEntity>` 구현 제공** — Protobuf 기반 직렬화
2. **SSOT 유지** — Excel이 유일한 스키마 원천
3. **Core 격리 유지** — Protobuf 참조가 이 프로젝트에만 존재
4. **플랫폼 독립성** — Unity/Server 모두 사용 가능

---

## Converter 책임 최소화

**Converter는 parse 실패/exception 사유만 반환하면 된다.**

모든 protobuf 기반 Converter는 `IEntityConverter<TEntity>` 계약을 구현해야 하며,
엔티티의 `_Load*/_Save*` 메소드를 호출하는 오케스트레이터 역할만 수행한다.

Converter가 엔티티 필드를 직접 만지는 것은 **금지**된다.

### Converter 책임 범위

| 책임 | Converter | Generator/Loader |
|------|:---------:|:----------------:|
| bytes → protobuf parse | ✅ | |
| parse 실패 시 예외/에러 반환 | ✅ | |
| 타입 검증 (Type Row2) | | ✅ |
| 범위 검증 (byte/short 등) | | ✅ |
| ref 대상 존재 검증 | | ✅ |
| Options 문법 검증 | | ✅ |

### Converter 동작 규칙 (MUST)

| # | 규칙 |
|---|------|
| 1 | **bytes → protobuf parse 성공/실패만 책임** |
| 2 | 실패 시 예외 타입/메시지/에러 코드를 **그대로 노출**(또는 래핑) |
| 3 | **의미 검증(범위/옵션/키 정책)은 Converter 책임이 아니다** |
| 4 | Converter는 **상세 검증 로직을 포함하지 않는다** |

### 에러 처리 예시

```csharp
public class ProtobufEntityConverter<TEntity> : IEntityConverter<TEntity>
{
    public IReadOnlyList<TEntity> FromBinary(byte[] bytes)
    {
        try
        {
            // protobuf parse만 수행
            return ParseFromBytes(bytes);
        }
        catch (InvalidProtocolBufferException ex)
        {
            // parse 실패 사유 그대로 전달
            throw new CoreException(CoreError.ParseFailed, ex.Message, ex);
        }
    }
}
```

### 책임 분리 원칙

> **"검증은 Generator/Loader, 변환은 Converter"**
> 
> - **Generator**: Excel→Proto 단계에서 타입/범위/ref 검증
> - **Loader**: 런타임 Load 시 추가 검증 (필요 시)
> - **Converter**: bytes ↔ Entity 변환만 담당

---

## Common 모듈

> Common 관련 규약/구현 지침은 `skills/devian-common/` 스킬을 참조한다.

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Excel → .proto 생성 산출물이 생긴다 |
| 2 | .proto → protoc → C# generated code 산출물이 생긴다 |
| 3 | 생성된 파일에 "DO NOT EDIT" 주석이 있다 |
| 4 | repo 전체에서 "proto가 SSOT"처럼 읽히는 문장이 없다 |
| 5 | `Devian.Core`는 Protobuf를 모른다 |
| 6 | **Converter는 parse 실패/exception 사유만 반환** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | **기반** — 직렬화 계약 IEntityConverter<TEntity> 제공 |
| `27-devian-friendly-format` | **입력** — Excel/JSON 입력 포맷 스펙 |
| `28-json-row-io` | **저장** — JSON 저장/로드 포맷 (NDJSON) |
| `61-tablegen-implementation` | **협력** — Excel → proto 생성, 강검증 |
| `24-table-authoring-rules` | **참조** — Excel 타입 규칙 |

---

## Converter Call Flow (정본 요약)

Devian의 protobuf 기반 직렬화에서 `IEntityConverter<TEntity>`는
엔티티 직렬화 파이프라인의 오케스트레이터 역할을 수행한다.

엔티티는 내부 전용 메소드만을 통해 직렬화되며,
Converter는 엔티티의 필드에 직접 접근하지 않는다.

엔티티가 제공해야 하는 내부 전용 메소드는 다음과 같다.

- `_LoadProto(TProto msg)`
- `_SaveProto(): TProto`
- `_LoadJson(string json)` — **디버그/테스트 전용**
- `_SaveJson(): string` — **디버그/테스트 전용**

Binary 직렬화 흐름은 다음과 같다.

- `FromBinary(bytes)`  
  → `MessageParser<TProto>`로 protobuf 메시지를 파싱  
  → `entity._LoadProto(proto)` 호출

- `ToBinary(entity)`  
  → `entity._SaveProto()` 호출  
  → `proto.ToByteArray()` 반환

JSON 직렬화는 protobuf JSON 규약을 사용하며,
`Google.Protobuf.JsonParser / JsonFormatter`를 통해 처리된다.

- `FromJson(json)`  
  → protobuf JSON 파싱  
  → `entity._LoadProto(proto)` 호출

- `ToJson(entity)`  
  → `entity._SaveProto()` 호출  
  → protobuf JSON 문자열 생성

Converter는 `_LoadProto/_SaveProto/_LoadJson/_SaveJson`만 호출하며,
엔티티 내부 필드에는 직접 접근하지 않는다.

> **주의:** `_LoadJson()`은 디버그/테스트 전용이다. Table loading pipeline에서는 사용하지 않는다.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
