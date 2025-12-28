# Devian – 41 WS Dispatcher Skeleton

## Purpose

**WebSocket 기반 "메시지 디스패처"의 뼈대를 정의한다.**

Dispatcher skeleton은 **예시 소비자 패턴**이며,  
통신 인프라/표준으로 오해되어서는 안 된다.

---

## Belongs To

**Consumer / Application**

> Dispatcher는 **Consumer 프로젝트**(NestJS/Unity)에서 구현한다.  
> Devian Core는 추상(PacketEnvelope, IPacketHandler)만 제공한다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| WebSocket 기반 소비자 예시 구조 | Dispatcher skeleton |
| Transport-agnostic 디스패처 | session 추상화 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| protocol 표준화 | ❌ 강제 없음 |
| server topology | ❌ consumer 결정 |
| transport 강제 | ❌ consumer 결정 |
| NestJS modules | ❌ consumer 구현 |
| application lifecycle | ❌ consumer 구현 |
| runtime orchestration | ❌ consumer 구현 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 디스패처는 **Transport를 모른다** (Transport-agnostic) |
| 2 | 디스패처는 **Consumer 프로젝트에서 구현**한다 |
| 3 | 단 하나의 입력 함수: `DispatchAsync(envelope, session, ct)` |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Skeleton은 **참고용**이다 |
| 2 | 다른 구현도 동일하게 허용 |
| 3 | 도메인별 독립 디스패처 권장 |

---

## Position in Architecture

```
[ ProtocolGen outputs ]       ← 22 (contracts/{language}/{domain}/generated)
              ↓
[ Dispatcher Skeleton ]       ← 41 (THIS SKILL, Consumer 구현)
              ↓
[ WS Transport Adapter ]      ← 40 (Consumer 구현)
```

---

## Core Contracts

### From Devian.Core (Provided)

```csharp
// Devian.Core에 정의됨
public readonly struct PacketEnvelope { ... }
public interface IPacketHandler<TReq, TRes> { ... }
public interface IPacketContext { ... }
```

### Consumer Implementation

```csharp
// Consumer 프로젝트에서 구현
public interface ITransportSession
{
    string SessionId { get; }
    Task SendAsync(PacketEnvelope envelope);
    Task CloseAsync(int? code = null, string? reason = null);
}

public interface IDispatcher
{
    Task DispatchAsync(
        PacketEnvelope inEnvelope, 
        ITransportSession session, 
        CancellationToken ct = default);
}
```

---

## Message Kind Decision

| Kind | 처리 방식 |
|------|----------|
| **Request** | 핸들러 실행 후 Response 생성 |
| **Response** | Pending request map에서 resolve |
| **Event** | 이벤트 핸들러 실행 (응답 없음) |

---

## Non-Goals

이 스킬은 다음을 다루지 않는다:

| 항목 | 담당 |
|------|------|
| WS 라이브러리 선택/구현 | 40 |
| raw 프레임 파싱/압축/핑퐁 | 40 |
| 인증/권한/세션 저장소 설계 | 별도 스킬 |
| 재연결 전략 | 별도 스킬 |

---

## Responsibilities

1. **예시 디스패처 패턴 제공** — 강제가 아닌 참고용
2. **Transport 분리 보장** — session 추상화
3. **도메인별 분리 허용** — AuthDispatcher, InGameDispatcher 등

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 이 문서를 안 써도 **Devian 사용 가능**해 보인다 |
| 2 | **"표준 WS 구조"로 오해되지 않는다** |
| 3 | Consumer 프로젝트에서 구현됨이 명확 |
| 4 | Transport-agnostic 디스패처 |
| 5 | WS 구현 없이 디스패처를 유닛 테스트 가능 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | **참조** — PacketEnvelope, IPacketHandler |
| `22-codegen-protocol-csharp-ts` | **상위** — Envelope/opcode/handler 계약 |
| `40-ws-transport-adapter` | **하위** — Transport 어댑터 |
| `52-app-templates-blueprint` | **소비자** — 앱 템플릿 |
| `62-protocolgen-implementation` | Protocolgen 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
