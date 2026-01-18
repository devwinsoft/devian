# Devian v10 — Common Feature: Variant

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Overview

Common 모듈에서 사용하는 Variant(Tagged Union) 타입을 정의한다.

Variant는 값의 타입(Int / Float / String)을 **CInt/CFloat/CString 기반으로 보존**하며,
DATA 테이블(예: TB_COMPLEX_POLICY)에서 fallback/min/max 같은 정책 값을 안전하게 표현하는 데 사용한다.

**Variant는 Complex feature에 의존한다.** 내부 값은 CInt/CFloat/CString(Complex shape)으로 저장되며, 테이블/직렬화에서 이 형식이 사용된다.

---

## Responsibilities

- Int / Float / String 3종 타입을 CInt/CFloat/CString 기반으로 표현하는 Variant 제공
- 타입 보존(숫자 파싱 추측 금지)
- 결정적(Deterministic) 직렬화 표현 정의 (JSON Tagged Union + Complex shape)
- 정책/테이블 로직이 Variant를 안전하게 사용할 수 있는 최소 API 제공

---

## Non-goals

- 임의 객체(object) 지원
- 자동 타입 추론/자동 캐스팅("적당히" 변환) — 금지
- DateTime, Guid 등 확장 타입 지원 (필요 시 별도 feature로 분리)

---

## Hard Rules (MUST)

1. **타입 추론 금지**
   - Variant는 생성 시점에 Kind가 확정되어야 한다.
   - 문자열/숫자 파싱으로 "알아서" Kind를 맞추지 않는다.

2. **암묵적 변환 금지**
   - Int → Float 자동 변환, Float → Int 자동 변환 같은 묵시적 변환을 제공하지 않는다.
   - 변환이 필요하면 호출자가 명시적으로 처리한다.

3. **결정적 JSON 표현(Tagged Union + Complex shape)**
   - Variant의 JSON 표현은 아래 규격을 따른다.
   - **원시값(raw number/string)으로 직렬화 금지** — 반드시 Complex shape 사용.

4. **비지원 Kind는 에러**
   - 지원하지 않는 Kind 또는 잘못된 JSON 표현은 조용히 처리하지 않고 명확히 실패(예외/에러)한다.

5. **Strict JSON Parsing**
   - `k`가 없거나 `'i'|'f'|'s'`가 아니면 예외
   - `k='i'`인데 `i`가 없으면 예외
   - `k='i'`인데 `f`/`s`가 같이 들어오면 예외
   - `k='i'`의 `i`는 `{save1,save2}` shape여야 하며, 둘 중 하나라도 없으면 예외
   - `k='s'`의 `s`는 `{data}` shape여야 하며 `data` 없으면 예외

---

## Internal Representation (Complex 기반)

Variant는 내부적으로 CInt/CFloat/CString을 사용하여 값을 저장한다.

### C# 내부 구조

```csharp
public readonly struct Variant
{
    private readonly VariantKind _kind;
    private readonly CInt _i;
    private readonly CFloat _f;
    private readonly CString _s;
}
```

### 설계 근거

- **Complex 통합:** 테이블 데이터와 동일한 형식으로 저장되어 일관성 유지
- **결정적 직렬화:** 테이블 빌더와 런타임 직렬화가 동일한 shape 사용
- **안전성:** 타입별 필드 분리로 잘못된 접근 방지

### 금지

- `byte[]` 캐시를 사용한 Union 스타일 구현 금지
- 원시값(`{"k":"i","i":123}`)으로 직렬화 금지 — 반드시 Complex shape 사용

---

## Table Input Format (Human-friendly)

Variant를 DATA 테이블(예: XLSX 셀)에서 입력할 때는 JSON을 사용하지 않는다.
테이블 셀에는 사람이 직접 입력 가능한 **단일 문자열 포맷**을 사용한다.

### Format

- Int: `i:<int>`
- Float: `f:<float>`
- String: `s:<string>`

Examples:

- `i:0`
- `i:10000`
- `f:0`
- `f:3.5`
- `s:AttackPower`
- `s:CriRate`

### Parsing Rules (Strict)

1. 셀 값은 문자열로 취급한다(Trim 후 파싱).
2. 접두사는 반드시 `i:` / `f:` / `s:` 중 하나여야 한다.
   - 다른 형식은 실패한다. (타입 추론 금지)
3. `i:` 뒤에는 정수만 허용한다.
   - 예: `i:-10` OK, `i:3.5` FAIL
4. `f:` 뒤에는 실수 표현을 허용한다.
   - 예: `f:0` OK, `f:3.5` OK
5. `s:` 뒤에는 문자열을 그대로 사용한다.

### NDJSON 출력

테이블 빌더는 Variant 셀을 다음 형태의 JSON 객체로 저장한다:

- `i:100` → `{ "k": "i", "i": { "save1": ..., "save2": ... } }`
- `f:3.5` → `{ "k": "f", "f": { "save1": ..., "save2": ... } }`
- `s:Hello` → `{ "k": "s", "s": { "data": "..." } }`

**Variant 컬럼은 NDJSON에서 `{k, i|f|s}` 오브젝트로 저장되며, `i`/`f`/`s`는 Complex shape(CInt/CFloat/CString)이다.**

---

## JSON Representation (Deterministic)

Variant는 다음 Tagged Union + Complex shape 형태의 JSON 오브젝트로 표현한다.

- Int:
  - `{ "k": "i", "i": { "save1": 123, "save2": 456 } }`
- Float:
  - `{ "k": "f", "f": { "save1": 123, "save2": 456 } }`
- String:
  - `{ "k": "s", "s": { "data": "Y3+HLPJx..." } }`

규칙:

- `k`는 반드시 `"i" | "f" | "s"` 중 하나
- 값 필드는 Kind에 맞는 키만 존재해야 한다.
- `i`/`f`는 `{ save1, save2 }` shape
- `s`는 `{ data }` shape (ComplexUtil 마스킹 후 base64 인코딩)
- 다른 키가 섞이거나, Kind와 값 필드가 불일치하면 실패한다.
- **절대 `{ "k": "i", "i": 123 }` 같은 원시값 형태로 저장하지 않는다.**

---

## Public API

### C# (`Devian.Module.Common`)

```csharp
namespace Devian.Module.Common
{
    public enum VariantKind : byte
    {
        Int = 1,
        Float = 2,
        String = 3,
    }

    // Immutable (readonly struct)
    // JsonConverter로 strict (de)serialize
    [JsonConverter(typeof(VariantJsonConverter))]
    public readonly struct Variant
    {
        public VariantKind Kind { get; }

        // Factory
        public static Variant FromInt(int value);
        public static Variant FromFloat(float value);
        public static Variant FromString(string value);

        // Raw factory (for deserialization)
        public static Variant FromRaw(CInt cint);
        public static Variant FromRaw(CFloat cfloat);
        public static Variant FromRaw(CString cstring);

        // Strict accessors (Kind 불일치 시 실패 또는 false 반환)
        public bool TryGetInt(out int value);
        public bool TryGetFloat(out float value);
        public bool TryGetString(out string value);

        // Debug-friendly
        public override string ToString();
    }
}
```

### TypeScript (`@devian/module-common/features`)

```typescript
// features/variant.ts

// CInt/CFloat shape
export interface CIntShape { save1: number; save2: number; }
export interface CFloatShape { save1: number; save2: number; }
export interface CStringShape { data: string; }

export type Variant =
  | { k: 'i'; i: CIntShape }
  | { k: 'f'; f: CFloatShape }
  | { k: 's'; s: CStringShape };

export function vInt(value: number): Variant;
export function vFloat(value: number): Variant;
export function vString(value: string): Variant;

// Raw factory (for deserialization)
export function vIntRaw(save1: number, save2: number): Variant;
export function vFloatRaw(save1: number, save2: number): Variant;
export function vStringRaw(data: string): Variant;

// Accessors (decode from Complex shape)
export function asInt(v: Variant): number;
export function asFloat(v: Variant): number;
export function asString(v: Variant): string;

export function isInt(v: Variant): v is { k: 'i'; i: CIntShape };
export function isFloat(v: Variant): v is { k: 'f'; f: CFloatShape };
export function isString(v: Variant): v is { k: 's'; s: CStringShape };
```

---

## Dependency Rules

- **Variant는 Complex feature에 의존한다.**
  - 내부 저장 및 직렬화에 CInt/CFloat/CString을 사용한다.
- 직렬화는 Newtonsoft.Json(C#) 및 표준 JSON(TS)을 사용한다.
- Unity 호환을 고려한다 (C#에서 System.Text.Json 사용 금지 정책 준수).

### Unity(UPM) 사용 시 필수 의존성

Unity 프로젝트에서 Variant를 사용하려면:

1. **package.json**: `com.unity.nuget.newtonsoft-json` 의존성 필요
2. **asmdef**: `references`에 `Newtonsoft.Json` 포함 필요

> 두 설정은 성격이 다르며 둘 다 필요하다. 상세 규약은 `skills/devian/19-unity-module-common-upm/SKILL.md` 참조.

---

## Examples

### C#

```csharp
using Devian.Module.Common;

var a = Variant.FromInt(100);
var b = Variant.FromFloat(3.5f);
var c = Variant.FromString("AttackPower");

if (a.TryGetInt(out var ai))
{
    // ai == 100
}

// JSON 직렬화
var json = JsonConvert.SerializeObject(a);
// → {"k":"i","i":{"save1":...,"save2":...}}

// JSON 역직렬화
var parsed = JsonConvert.DeserializeObject<Variant>(json);
```

### TypeScript

```typescript
import { vInt, vFloat, vString, asInt, asFloat, isFloat } from '@devian/module-common/features';

const a = vInt(100);
// a = { k: 'i', i: { save1: ..., save2: ... } }

const b = vFloat(3.5);
const c = vString('AttackPower');

if (isFloat(b)) {
  const val = asFloat(b); // 3.5
}
```

---

## DoD (Definition of Done)

- [x] 스킬 문서 ACTIVE 전환 + Complex 기반 Internal Representation 명시
- [x] C# Variant 구현이 CInt/CFloat/CString 기반으로 변경됨
- [x] C# VariantJsonConverter로 strict (de)serialize
- [x] TS variant.ts가 Complex shape 기반으로 변경됨
- [x] Table Builder가 Variant 타입을 Complex shape로 출력
- [x] features/index.ts export 반영
- [x] Unity 패키지 동기화 (com.devian.module.common)
- [x] JSON 예제가 모두 Complex shape 기반 (원시값 예제 제거)

---

## Reference

- Domain Policy: `skills/devian-common/00-domain-policy/SKILL.md`
- Module Policy: `skills/devian-common/01-module-policy/SKILL.md`
- Complex Feature: `skills/devian-common/13-feature-complex/SKILL.md`
- TableGen EnumGen: `skills/devian/63-tablegen-enumgen/SKILL.md`
