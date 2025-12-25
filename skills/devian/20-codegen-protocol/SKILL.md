# Devian – 20 Codegen: Protocol Generation

## Purpose

**Describes the protocol and boundaries used by code generation.**

IDL 기반으로 **C# 프로토콜 계약 코드를 생성**한다.

- 프로토콜 계약 코드는 `contracts/csharp/{domain}`로 생성된다
- Transport/Dispatcher 구현은 생성하지 않는다

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| codegen 단계 간 인터페이스 | IDL → generated code |
| 입력/출력 계약 | 경로, 형식 정의 |
| DTO / opcode / handler interface | 메시지 타입 생성 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| runtime orchestration | ❌ 실행 관리 없음 |
| execution engine | ❌ 실행 엔진 없음 |
| server lifecycle | ❌ 서버 생명주기 없음 |
| transport | ❌ Consumer 책임 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | codegen은 `contracts/` 내부에서만 산출물 생성 |
| 2 | codegen은 서버 구조를 가정하지 않는다 |
| 3 | transport와 완전히 분리 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | generated 코드는 재생성 가능해야 함 |
| 2 | 순수 타입(shape)만 제공 |

---

## Inputs

| Input | 설명 |
|-------|------|
| `input/protocols/*` | IDL source of truth |
| domainName | 대상 도메인 |

---

## Outputs

| Path | 용도 |
|------|------|
| `contracts/csharp/{domain}/generated/` | codegen 산출물 |
| `contracts/csharp/{domain}/src/` | (선택) 수동 코드 영역 |

---

## Dependency Rules

### Generated protocol code MAY reference:

| Target | 허용 |
|--------|:----:|
| Devian.Core | ✅ |
| contracts/csharp/common | ✅ |

### Generated protocol code MUST NOT reference:

| Target | 금지 |
|--------|:----:|
| Devian.Tools | ❌ |
| UnityEngine | ❌ |
| 다른 도메인 (common 제외) | ❌ |

---

## Generation Scope

### Included

| 항목 | 설명 |
|------|------|
| DTO | 메시지 데이터 타입 |
| opcode/message id | 메시지 식별자 |
| handler interface | (선택) 핸들러 계약 |
| serialization-friendly shapes | 직렬화 가능 구조 |

### NOT Included

| 항목 | 이유 |
|------|------|
| transport | Consumer 책임 |
| NestJS integration | 서버 자율 |
| WS server/client templates | Consumer 책임 |

---

## Responsibilities

1. **IDL → C# DTO 변환** — 프로토콜 타입 생성
2. **codegen 경로 규칙 정의** — `contracts/csharp/{domain}/generated`
3. **transport 분리 보장** — 순수 계약 코드만 생성

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 출력 경로가 `contracts/csharp/{domain}/generated`로만 출력 |
| 2 | codegen 산출물에 도메인 프로토콜 코드가 Devian 패키지 밖에 있음 |
| 3 | transport와 완전히 분리 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `22-codegen-protocol-csharp-ts` | C#/TS 생성 |
| `26-domain-scaffold-generator` | 도메인 뼈대 |
| `51-generated-integration` | Generated 통합 |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-21 | 표준 템플릿 적용, 용어 정리 |
| 0.2.0 | 2024-12-21 | C# contracts output |
| 0.1.0 | 2024-12-20 | Initial skill definition |
