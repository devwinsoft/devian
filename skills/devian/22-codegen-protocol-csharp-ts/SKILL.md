# Devian – 22 Codegen: Protocol C# ↔ TypeScript

## Purpose

**Specifies language-specific protocol details for C# and TypeScript code generation.**

동일한 프로토콜 정의(IDL)로부터 **C#과 TypeScript 계약 코드**를 생성한다.

- 출력 경로는 반드시 `contracts/{language}/{domain}`을 따른다
- TS 산출물은 NestJS/Browser에서 "그냥 가져다 쓰는 순수 계약 패키지"여야 한다
- Transport(WebSocket/HTTP/NestJS 구현)는 생성하지 않는다

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 언어별 codegen 차이 | C# vs TypeScript |
| 출력 형태의 차이 | csproj vs package.json |
| 패키지 구조 | 즉시 import 가능한 형태 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| 언어 간 공유 runtime | ❌ 존재하지 않음 |
| cross-language abstraction | ❌ 각 언어 독립 |
| transport implementation | ❌ Consumer 책임 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | C# / TS는 논리적으로 동일한 message schema를 가진다 |
| 2 | enum 값, 필드 명, optional 규칙이 동일해야 한다 |
| 3 | transport 계층이 생성물에 포함되지 않는다 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | TS 패키지는 NestJS 의존성 없이 import 가능해야 함 |
| 2 | 두 언어 모두 순수 타입(shape)만 제공 |

---

## Inputs

- `input/protocols/*` (IDL source of truth)
- domain selection (예: auth, ingame)

---

## Outputs

### C#

| Path | 용도 |
|------|------|
| `contracts/csharp/{domain}/generated/` | codegen 산출물 |
| `contracts/csharp/{domain}/src/` | (선택) hand-written 영역 |

### TypeScript (Optional)

> ⚠️ **TypeScript codegen은 optional이다.**  
> 활성화된 경우에만 아래 경로에 생성된다.

| Path | 용도 |
|------|------|
| `contracts/ts/{domain}/generated/` | codegen 산출물 |
| `contracts/ts/{domain}/src/` | barrel export |
| `contracts/ts/{domain}/src/index.ts` | 진입점 |
| `contracts/ts/{domain}/package.json` | 패키지 정의 |

**TS codegen이 비활성화된 경우, `contracts/ts/{domain}`은 존재하지 않을 수 있다.**

---

## TS Package Minimum Contract

### package.json

```json
{
  "name": "@devian/{domain}",
  "version": "0.1.0",
  "main": "src/index.ts",
  "types": "src/index.ts"
}
```

### src/index.ts

```typescript
export * from '../generated';
```

---

## What This Skill Generates

| 항목 | 설명 |
|------|------|
| Message DTO | 메시지 타입 정의 |
| Request/Response types | 요청/응답 타입 |
| Opcode / MessageId enum | 메시지 식별자 |
| (optional) minimal helpers | type guards 등 |

---

## What This Skill Does NOT Generate

| 항목 | 이유 |
|------|------|
| WebSocket glue | Transport 책임 |
| NestJS decorators / gateway helpers | 서버 자율 |
| Unity bindings | Consumer 책임 |
| Dispatcher runtime | 41에서 별도 |

---

## Responsibilities

1. **언어별 codegen 규칙 정의** — C# / TS 각각의 출력 형태
2. **schema parity 보장** — 두 언어 간 동일한 논리 구조
3. **TS optional 정책과 조화** — Bundle C 정책 준수

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | C# 출력이 `contracts/csharp/{domain}` 아래에 생성 |
| 2 | TS 출력이 `contracts/ts/{domain}` 아래에 생성 (활성화 시) |
| 3 | 특정 언어가 "기준 언어"처럼 보이지 않는다 |
| 4 | Bundle C의 TS optional 정책과 충돌하지 않는다 |
| 5 | Transport 계층이 생성물에 포함되지 않음 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `20-codegen-protocol` | C# codegen |
| `21-codegen-table` | Table codegen |
| `26-domain-scaffold-generator` | 도메인 뼈대 |
| `51-generated-integration` | Generated 통합 |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-21 | 표준 템플릿 적용, TS optional 명시, 용어 정리 |
| 0.2.0 | 2024-12-21 | Language-first outputs |
| 0.1.0 | 2024-12-20 | Initial skill definition |
