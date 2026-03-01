# 44-protocolgen-implementation

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

Protocol generator의 구현을 “문서로 재서술”하지 않기 위한 가드 문서다.

이 스킬은 **정책적으로 필요한 검증 포인트만** 남기고,
구체적인 구현/산출 API는 모두 **런타임/제너레이터 코드**로 위임한다.

---

## Implementation is Code Truth

아래 항목은 SKILL이 아니라 **코드/Reference가 정답**이다.

- 생성되는 C#/TS 코드 구조, 클래스/타입 이름
- codec의 정확한 인코딩/디코딩 구현
- 프레임 포맷(바이트 레이아웃)
- “sender / transport” 계층 분리 방식과 인터페이스 시그니처

SKILL은 위 내용을 단정해서는 안 된다.

---

## Policy-level Checks (MUST)

1) ProtocolSpec 파일은 JSON이다.
2) opcode/tag는 결정적이어야 한다.
3) Tag reserved range(19000..19999)는 금지다.
4) Registry 파일(opcodes/tags)은 키 정렬 등으로 **결정적으로 저장**되어야 한다.
5) 생성되는 모든 PROTOCOL 모듈은 Common 모듈을 **무조건 참조**해야 한다.
   - Common 참조 판정은 하지 않는다.
   - C#: csproj ProjectReference + `*.g.cs`에 `using Devian;`
   - TS: package.json dependencies에 `@devian/module-common`

---

## Complex Type Aliases (cint / cfloat / cstring)

프로토콜 스펙에서 Complex 타입을 사용하기 위한 **별칭(alias)** 정책이다.

### 지원 별칭

| 별칭 | 설명 |
|------|------|
| cint | 마스킹된 정수 (masking only) |
| cfloat | 마스킹된 실수 (masking only) |
| cstring | 마스킹된 문자열 (masking only) |

### JSON Shape (직렬화 표현)

| 별칭 | JSON 표현 |
|------|-----------|
| cint | object: save1(int), save2(int) |
| cfloat | object: save1(int), save2(int) |
| cstring | object: data(string) |

### 언어별 타입 매핑

**C#:**

| 별칭 | 매핑 타입 | 비고 |
|------|-----------|------|
| cint | Devian.CInt | struct (value type) |
| cfloat | Devian.CFloat | struct (value type) |
| cstring | Devian.CString | struct (value type) |

**TypeScript:**

| 별칭 | 매핑 타입 (shape) |
|------|-------------------|
| cint | { save1: number; save2: number } |
| cfloat | { save1: number; save2: number } |
| cstring | { data: string } |

TS에서는 클래스가 아닌 shape 타입으로 취급한다.

### 코덱 정책 (MUST)

1) 프로토콜 파일 내에 cint/cfloat/cstring이 **1개라도 있으면** 기본 코덱은 **JSON**이다.
2) protobuf 코덱은 별칭 타입을 지원하지 않는다.
3) protobuf 코덱으로 별칭 포함 메시지를 encode/decode 시도하면 **런타임에서 명확히 실패**한다 (NotSupportedException).
4) CodecJson은 Newtonsoft.Json(JsonConvert)을 사용한다.

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드