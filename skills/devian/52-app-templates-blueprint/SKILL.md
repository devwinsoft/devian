# Devian – 52 App Templates Blueprint

## Purpose

**예시 애플리케이션 구성 패턴을 제공한다.**

Blueprint는 **참고 템플릿**이며, App 구조 강제가 아니다.

---

## Belongs To

**Consumer / Application**

> 이 문서는 소비자 레벨 문서다.  
> "이 템플릿을 따라야 한다"는 인상을 주지 않는다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 예시 애플리케이션 구성 | NestJS, Unity, Browser |
| Devian + Domain 사용 패턴 | 대표적인 조합 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| production architecture | ❌ consumer 결정 |
| lifecycle enforcement | ❌ consumer 결정 |
| server architecture | ❌ consumer 결정 |
| NestJS modules | ❌ consumer 구현 |
| deployment / infra | ❌ consumer 결정 |
| generated code ownership | ❌ domain 소유 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 모든 App은 **Domain을 중심으로** 구성된다 |
| 2 | Devian은 직접 참조 대상이 아니라, **Domain 뒤에 숨어 있다** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Blueprint는 **참고용**이다 |
| 2 | 소비 방식은 프로젝트마다 **달라도 된다** |
| 3 | transport와 UI는 자유롭게 구현 |

> 모든 항목이 **권장(SHOULD)** 수준이다.

---

## What This Blueprint IS / IS NOT

### IS

- Devian + Domain을 사용하는 **대표적인 App 구성 패턴**
- Consumer 관점의 설계 예시
- 실제 현업에서 바로 쓸 수 있는 조합

### IS NOT

- Devian이 제공하는 앱 런타임
- Devian 강제 아키텍처
- Devian 전용 서버/클라 스캐폴드

---

## Canonical App Templates

### 1) NestJS WebSocket Server

```
NestJS App
├── ws transport adapter (40)
├── dispatcher (41)
├── domain runtime
│   ├── protocol DTO
│   ├── table loader
│   └── parser registry
└── business logic
```

**NestJS 책임:** transport, lifecycle, DI

### 2) Unity Client

```
Unity App
├── Devian.Unity (raw fetch)
├── domain runtime
│   ├── table loader
│   └── protocol DTO
└── gameplay / UI
```

**Unity 책임:** raw asset loading, rendering, input

### 3) Browser Test Client (QA)

- TypeScript protocol DTO (22)
- Simple WebSocket / HTTP client
- 최소 코드, 빠른 반복

---

## What Was Explicitly Removed

| 제거된 템플릿 | 이유 |
|--------------|------|
| ❌ Devian ws-client | transport는 소비자 책임 |
| ❌ Devian ws-server | 비대화 방지 |
| ❌ Devian web-client | consumer 영역 |
| ❌ Devian 전용 socket server | consumer 영역 |

---

## Responsibilities

1. **참고 템플릿 제공** — 강제가 아닌 예시
2. **Domain 중심 구조 설명** — 모든 앱의 중심은 Domain
3. **책임 분리 명시** — transport / UI / runtime

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | **"이 템플릿을 따라야 한다"는 인상이 없다** |
| 2 | **소비자 레벨 문서**임이 즉시 드러난다 |
| 3 | Devian이 앱 프레임워크처럼 보이지 않는다 |
| 4 | 모든 템플릿의 중심이 Domain이다 |
| 5 | transport / UI / runtime 책임이 명확히 분리된다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `50-consumption-blueprint` | 소비 구조 |
| `22-codegen-protocol-csharp-ts` | Protocol codegen |
| `40-ws-transport-adapter` | Transport adapter |
| `41-ws-dispatcher-skeleton` | Dispatcher |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
