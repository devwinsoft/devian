# Devian – 50 Consumption Blueprint

## Purpose

**Provides reference consumption patterns for generated contract artifacts.**

이 문서는 Devian 기반 프로젝트에서 "소비(Consumption)" 단계의 **참고용 패턴**을 설명한다.

> Blueprint는 **권장 소비 패턴**이지, **강제 구조가 아니다**.  
> 소비 방식은 프로젝트마다 달라도 된다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| generated 코드 소비 예시 | Table, Protocol 소비 패턴 |
| domain runtime 내부 사용 패턴 | Loader, Parser, Dispatcher |
| End-to-End Flow | Authoring → Consumer App |

### Out of Scope

| 항목 | 설명 |
|------|------|
| application architecture enforcement | ❌ 앱 구조 강제 없음 |
| server scaffolding | ❌ 서버 템플릿 없음 |
| release pipeline | ❌ Unity Consumer 책임 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Devian.Tools는 validate / codegen만 수행 |
| 2 | Release 바이너리 생성은 Consumer 책임 |
| 3 | 모든 런타임 코드는 Domain 소유 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Blueprint는 참고용이다 |
| 2 | 소비 방식은 프로젝트마다 달라도 된다 |
| 3 | Protocol만 사용 / Table만 사용 모두 가능 |

---

## End-to-End Flow

```
Authoring (input/tables, input/protocols)
        ↓
Devian.Tools (validate / codegen)
        ↓
Domain Runtime (contracts/{language}/{domain})
  - table loader
  - parser registry
  - protocol DTO
        ↓
Consumer App (Unity / NestJS / Browser)
```

---

## Consumption Scope

### What Consumer Owns

| 항목 | 설명 |
|------|------|
| Domain 초기화 | DomainInit.cs 호출 |
| Parser registry 연결 | 도메인별 파서 등록 |
| Table loader 호출 | raw data → typed row 변환 |
| Transport 구현 | WebSocket / HTTP / etc |
| Dispatcher 구현 | opcode 라우팅 |
| Release 바이너리 생성 | Unity 에디터 파이프라인 |

### What Consumer Does NOT Own

| 항목 | 설명 |
|------|------|
| Authoring 규칙 해석 | Tools 책임 |
| Codegen | Tools 책임 |
| Validation | Tools 책임 |

---

## Input Flow

### Authoring Sources

```
input/
├─ protocols/    (IDL source of truth)
└─ tables/       (JSON authoring source of truth)
```

### Tools Output

```
contracts/{language}/{domain}/generated/
├─ Protocol/     (DTO, opcode, handler interface)
└─ Tables/       (row types, schema)
```

---

## Consumer Integration Points

### 1) Table Consumption

```csharp
// Consumer: JSON 로드
var json = await LoadNdjsonAsync("tables/items");
TB_Items.LoadFromJson(json);

// Consumer: Base64 로드
var base64 = await LoadBase64Async("tables/items");
TB_Items.LoadFromBase64(base64);

// 조회
var item = TB_Items.Get(1001);
```

### 2) Protocol Consumption

```csharp
// Domain dispatcher 사용
dispatcher.Register<LoginRequest>(OnLoginRequest);
dispatcher.Dispatch(envelope);
```

### 3) Parser Registry

```csharp
// DomainInit에서
ParserRegistry.Register<GFloat2>(new GFloat2Parser());
ParserRegistry.Register<ItemId>(new ItemIdParser());
```

---

## Flexibility Rules

| 규칙 | 설명 |
|------|------|
| Protocol만 사용 | table 없이 메시지 계약만 사용 가능 |
| Table만 사용 | protocol 없이 테이블 데이터만 사용 가능 |
| Dev JSON 직접 소비 | 개발 단계에서 JSON 직접 파싱 가능 |

---

## Responsibilities

1. **참고용 소비 패턴 제공** — 강제가 아닌 가이드
2. **End-to-End Flow 설명** — Authoring → Consumer
3. **Consumer 자유 보장** — Transport/Dispatcher 선택 자유

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 이 문서를 따라야만 Devian을 쓸 수 있다는 인상을 주지 않는다 |
| 2 | "템플릿 = 강제"로 오해되지 않는다 |
| 3 | Tools 역할이 validate / codegen만으로 한정 |
| 4 | Release 책임이 Unity Consumer에 있음이 명확 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `00-rules-minimal` | 모듈 구조 |
| `01-devian-core-philosophy` | 철학 원칙 |
| `26-domain-scaffold-generator` | 도메인 생성 |
| `28-json-row-io` | 테이블 JSON I/O 정본 |
| `51-generated-integration` | Generated 통합 |
| `52-app-templates-blueprint` | App 템플릿 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
