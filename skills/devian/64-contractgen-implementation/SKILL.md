# Devian – 64 Contractgen Implementation

## Purpose

**중립 Contract Spec(JSON)으로부터 C#/TS 타입 산출물(value types, structs, enums)을 생성한다.**

이 스킬은 `build.json`을 읽고 실제 코드를 생성하는 **구현 스펙**이다.

---

## Belongs To

**Consumer / Tooling (Build tool)**

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Contract Spec(JSON) 스키마 정의 | domain, namespace, types |
| kind별 타입 생성 | `value`, `struct`, `enum` |
| C# 타입 생성 | `{CS_OUT}/{domain}/Runtime/generated/*.cs` |
| TS 타입 생성 (optional) | `{TS_OUT}/{domain}/generated/*.ts` |

### Out of Scope

| 항목 | 설명 |
|------|------|
| Protocol Spec (messages) 처리 | ❌ 62 스킬 |
| 테이블 row type 생성 | ❌ 61 스킬 |
| 런타임 로더/파서 구현 | ❌ consumer 영역 |

---

## Inputs

| Input | 설명 |
|-------|------|
| `input/contracts/{domain}/*.json` | Contract Spec 원천 |
| `build.json` | `inputs.contracts` |
| `build.json` | `domains[domain].contractsSpec` |
| `build.json` | `targets.cs/ts.output`, `variables.CS_OUT/TS_OUT` |

---

## Outputs

| 타겟 | 출력 경로 |
|------|----------|
| C# | `{CS_OUT}/{domain}/Runtime/generated/*.cs` |
| TS (optional) | `{TS_OUT}/{domain}/generated/*.ts` |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 입력은 **`input/contracts/{domain}/*.json`**만 사용 |
| 2 | 출력 경로는 **`build.json`의 targets 템플릿을 그대로** 따른다 |
| 3 | core runtime은 **generated에 의존하지 않는다** (consumer가 import) |
| 4 | 같은 input → 같은 output (**결정적 빌드**) |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 타입 참조 실패 시 명확한 에러 메시지 (어떤 파일/타입) |
| 2 | 생성 파일 상단에 `DO NOT EDIT` 헤더 |
| 3 | cross-file 참조는 같은 domain/namespace 내에서만 (초기 단순화) |

---

## Contract Spec JSON Schema (확정)

### 파일 위치

```
input/contracts/{domain}/*.json
```

### 스키마

```json
{
  "domain": "common",
  "namespace": "Common",
  "types": [
    {
      "name": "ItemId",
      "kind": "value",
      "base": "int32",
      "doc": "Unique item identifier"
    },
    {
      "name": "ItemInfo",
      "kind": "struct",
      "doc": "Basic item information",
      "fields": [
        { "name": "id", "type": "ItemId" },
        { "name": "name", "type": "string" },
        { "name": "rarity", "type": "int32" },
        { "name": "tags", "type": "string[]", "optional": true }
      ]
    },
    {
      "name": "Rarity",
      "kind": "enum",
      "doc": "Item rarity levels",
      "values": [
        { "name": "Common", "value": 0 },
        { "name": "Rare", "value": 1 },
        { "name": "Epic", "value": 2 },
        { "name": "Legendary", "value": 3 }
      ]
    }
  ]
}
```

### 필드 설명

| 필드 | 필수 | 설명 |
|------|------|------|
| `domain` | ✅ | 도메인 이름 |
| `namespace` | ✅ | 생성될 코드의 네임스페이스 |
| `types` | ✅ | 타입 배열 |
| `types[].name` | ✅ | 타입 이름 |
| `types[].kind` | ✅ | `value`, `struct`, `enum` 중 하나 |
| `types[].doc` | ❌ | 문서 주석 |
| `types[].base` | ❌ | (value) 기본 타입 |
| `types[].fields` | ❌ | (struct) 필드 배열 |
| `types[].values` | ❌ | (enum) 값 배열 |

---

## Kind별 생성 규칙

### `value` — Value Type

```json
{ "name": "ItemId", "kind": "value", "base": "int32" }
```

**C# 출력:**
```csharp
public readonly record struct ItemId(int Value);
```

**TS 출력:**
```typescript
export type ItemId = number;
```

---

### `struct` — Struct Type

```json
{
  "name": "ItemInfo",
  "kind": "struct",
  "fields": [
    { "name": "id", "type": "ItemId" },
    { "name": "name", "type": "string" },
    { "name": "tags", "type": "string[]", "optional": true }
  ]
}
```

**C# 출력:**
```csharp
public sealed record ItemInfo(
    ItemId Id,
    string Name,
    string[]? Tags = null
);
```

**TS 출력:**
```typescript
export interface ItemInfo {
  id: ItemId;
  name: string;
  tags?: string[];
}
```

---

### `enum` — Enum Type

```json
{
  "name": "Rarity",
  "kind": "enum",
  "values": [
    { "name": "Common", "value": 0 },
    { "name": "Rare", "value": 1 }
  ]
}
```

**C# 출력:**
```csharp
public enum Rarity
{
    Common = 0,
    Rare = 1
}
```

**TS 출력:**
```typescript
export enum Rarity {
  Common = 0,
  Rare = 1
}
```

---

## Type Mapping

| Contract Type | C# | TypeScript |
|---------------|-----|------------|
| `string` | `string` | `string` |
| `int32` | `int` | `number` |
| `int64` | `long` | `number` |
| `float` | `float` | `number` |
| `double` | `double` | `number` |
| `bool` | `bool` | `boolean` |
| `T[]` | `T[]` | `T[]` |
| `CustomType` | 같은 namespace 내 참조 | 같은 방식 |

---

## Responsibilities

1. **Contract Spec(JSON) → C#/TS 타입 생성 알고리즘 구현**
2. **kind별(value/struct/enum) 코드 생성**
3. **build.json 템플릿 경로 준수**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Contract Spec 1개로 C#/TS 타입 파일 생성 |
| 2 | value/struct/enum 모두 정상 생성 |
| 3 | 타입 참조 실패 시 빌드 **실패 + 원인 출력** |
| 4 | build.json 템플릿 경로에 **정확히 출력** |
| 5 | TS가 비활성화된 도메인에서는 **TS 산출물 생성하지 않음** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `63-build-runner` | 빌드 실행기 |
| `62-protocolgen-implementation` | Protocol Spec 처리 |
| `61-tablegen-implementation` | Table Spec 처리 |
| `60-build-pipeline` | 빌드 스펙 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0 | 2024-12-21 | Initial skill definition |
