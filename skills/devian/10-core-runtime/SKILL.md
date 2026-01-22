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

Devian 런타임은 크게 세 계층으로 나눈다.

1) **Core (빌드/공용 유틸 레이어)**
- 도메인/프로토콜 생성물이 공통으로 의존할 수 있는 최소 유틸

2) **Protobuf (인코딩/디코딩 레이어)**
- Protobuf 스타일의 wire encoding/decoding 및 DFF 파서 제공
- ".proto/protoc 체인"을 전제하지 않는다 (v10은 IDL이 JSON 기반)

3) **Network (전송 레이어)**
- Transport 어댑터가 구현해야 하는 계약(Contract)
- WebSocket/TCP/UDP 등 구체 구현은 Consumer 책임

---

## Framework Modules

### C#

| 모듈 | 경로 | 역할 |
|------|------|------|
| Devian.Core | `framework-cs/module/Devian.Core/` | IEntity, ITableContainer, LoadMode 등 |
| Devian.Protobuf | `framework-cs/module/Devian.Protobuf/` | DFF 파서, Protobuf 직렬화 |
| Devian.Network | `framework-cs/module/Devian.Network/` | IPacketSender, PacketEnvelope |

### TypeScript

| 모듈 | 경로 | 역할 |
|------|------|------|
| devian-core | `framework-ts/module/devian-core/` | IEntity, ITableContainer, ICodec, LoadMode |
| devian-protobuf | `framework-ts/module/devian-protobuf/` | DffConverter, ProtobufCodec, IProtoEntity |

---

## Dependency Rules

Hard Rules (MUST)

1) 생성물(framework/**/generated)은 **Core/Protobuf/Network 중 필요한 레이어에만 의존**한다.
2) Network는 "전송"만 다룬다. 프로토콜/도메인 타입을 직접 알지 않는다.
3) Protobuf는 "표현/파싱"만 다룬다. Network를 참조하지 않는다.

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
