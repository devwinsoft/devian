# Devian v10 — Core Runtime Policy

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian 런타임 모듈의 **역할 분리(계층)**와 **의존성 정책**을 정의한다.

이 문서는 "어떤 런타임이 어떤 책임을 갖는가"만 서술한다.
구체적인 타입/시그니처/파일 목록은 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Runtime Layers (정책)

Devian 런타임은 크게 세 계층으로 나눈다. **모든 계층이 `namespace Devian` 단일을 사용한다.**

1) **Core (빌드/공용 유틸 레이어)** — namespace: `Devian`
- 도메인/프로토콜 생성물이 공통으로 의존할 수 있는 최소 유틸

2) **Proto (인코딩/디코딩 레이어)** — namespace: `Devian`
- Protobuf 스타일의 wire encoding/decoding 및 DFF 파서 제공
- ".proto/protoc 체인"을 전제하지 않는다 (v10은 IDL이 JSON 기반)
- 타입명: `Dff*`, `Protobuf*`, `IProto*` 등 기존 이름 유지

3) **Net (전송 레이어)** — namespace: `Devian`
- Transport 어댑터가 구현해야 하는 계약(Contract)
- WebSocket/TCP/UDP 등 구체 구현은 Consumer 책임
- 타입명: `Net` 접두사 규칙 적용 (예: `NetClient`, `NetWsClient`, `NetPacketEnvelope`)

> **분리 네임스페이스 금지:** 런타임 코드에서 분리된 하위 네임스페이스 사용 금지.
> 파일 폴더 구조(Core/, Proto/, Net/)는 유지하되 namespace는 모두 `Devian`이다.

---

## Framework Modules (단일 모듈 구조)

### C# — 단일 모듈

Devian C# 런타임은 단일 모듈(`Devian.csproj`)로 통합되어 있다.
**모든 파일은 `namespace Devian` 단일을 사용한다.**

| 경로 | namespace | 역할 |
|------|-----------|------|
| `framework-cs/module/Devian/src/Core/` | `Devian` | IEntity, ITableContainer, LoadMode 등 |
| `framework-cs/module/Devian/src/Proto/` | `Devian` | Dff 파서, Protobuf 직렬화 |
| `framework-cs/module/Devian/src/Net/` | `Devian` | INetPacketSender, NetPacketEnvelope 등 |

### TypeScript — 단일 패키지

Devian TS 런타임은 단일 패키지(`@devian/core`)로 통합되어 있다.

| 경로 | 역할 |
|------|------|
| `framework-ts/module/devian/src/` | IEntity, ITableContainer, ICodec, LoadMode |
| `framework-ts/module/devian/src/proto/` | DffConverter, ProtobufCodec, IProtoEntity |
| `framework-ts/module/devian/src/net/` | NetworkClient, NetworkServer, WsTransport |

---

## Dependency Rules

Hard Rules (MUST)

1) 생성물(framework/**/generated)은 **Core/Proto/Net 중 필요한 레이어에만 의존**한다.
2) Net은 "전송"만 다룬다. 프로토콜/도메인 타입을 직접 알지 않는다.
3) Proto는 "표현/파싱"만 다룬다. Net을 참조하지 않는다.

Soft Rules (SHOULD)

- 런타임 API는 **Span/ReadOnlySpan 기반**을 선호한다(불필요한 ToArray/MemoryStream 방지).

---

## Generated ↔ Runtime 경계

- Generated 코드는 **런타임 계약을 소비(consuming)하는 쪽**이다.
- 런타임 계약의 정답(타입/시그니처)은 Reference에서 확인한다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
