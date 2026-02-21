# devian/10-module — Overview

Devian 프레임워크의 핵심 정책, SSOT, 런타임, 직렬화, 네트워크 어댑터를 담당한다.

- **SSOT**: 문서/코드 정합성의 단일 정본
- **Core Runtime**: 언어 공통 런타임 인터페이스, 유틸리티, Variable 타입
- **Proto**: Protobuf 기반 직렬화 정책
- **Net**: WebSocket, HTTP-RPC 클라이언트/서버, WebGL 브릿지

---

## Sub-groups

| Sub-group | Description | Maps to |
|-----------|-------------|---------|
| [20-core](../20-core/00-overview/SKILL.md) | Core 런타임 (Crypto, Logger, Variable) | `src/Core/`, `src/Variable/` |
| [40-proto](../40-proto/00-overview/SKILL.md) | Serializer/Protobuf 정책 | `src/Proto/` |
| [60-net](../60-net/00-overview/SKILL.md) | Network Adapters | `src/Net/` |

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | 런타임 레이어 정책 + 의존성 규칙 |
| [03-ssot](../03-ssot/SKILL.md) | SSOT (Single Source of Truth) |

## Cross-cutting Policies

| Document | Description |
|----------|-------------|
| [04-unity-csharp-compat](../04-unity-csharp-compat/SKILL.md) | Unity C# 문법/언어버전 제한 |
| [05-generated-integration](../05-generated-integration/SKILL.md) | Generated 코드 통합 정책 |

---

## Related

- [SSOT](../03-ssot/SKILL.md)
- [Devian Index](../../SKILL.md)
