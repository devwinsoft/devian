# Devian v10 — Network Server (TypeScript)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

TypeScript 기반 네트워크 서버 모듈의 설계 원칙과 책임 분리를 정의한다.

---

## 설계 원칙

### 1. 공용 모듈과 그룹별 런타임 분리

- **devian-network-server**: 공용 코드만 포함
  - WebSocket transport (세션 관리, binary send)
  - Frame 파싱 (int32 LE opcode + payload)
  - Tagged BigInt JSON codec
  - ProtocolServer (runtime 주입)

- **devian-protocol-{group}**: 그룹별 서버 런타임 제공
  - Inbound opcode 이름 조회
  - Inbound dispatch (stub.dispatch)
  - Outbound proxy 생성

### 2. Unknown Opcode 정책

Unknown inbound opcode는:
- **절대 disconnect/close 하지 않는다**
- `onUnknownInboundOpcode` 훅으로 위임한다
- 기본 동작은 warn 로그만

---

## 디렉토리 구조

```
framework-ts/modules/devian-network-server/
├── src/
│   ├── index.ts              # 모듈 exports
│   ├── shared/
│   │   ├── frame.ts          # 프레임 파싱/생성
│   │   └── codec.ts          # Tagged BigInt JSON codec
│   ├── transport/
│   │   ├── ITransport.ts     # Transport 인터페이스
│   │   └── WsTransport.ts    # WebSocket 구현
│   └── protocol/
│       ├── IProtocolRuntime.ts   # 런타임 인터페이스
│       └── ProtocolServer.ts     # 프로토콜 서버
```

---

## 책임 분리

| 구성요소 | 책임 | 금지 |
|----------|------|------|
| WsTransport | 세션 관리, binary 송수신 | opcode 해석 |
| ProtocolServer | frame 파싱, runtime 호출 | disconnect on unknown |
| IServerProtocolRuntime | opcode 조회, dispatch, proxy 생성 | - |
| ExampleNetworkServer | 조립 + 핸들러 등록 | 비즈니스 로직 확장 |

---

## 예제 앱

위치: `framework-ts/apps/ExampleNetworkServer/`

예제 앱은 **조립 + 핸들러 등록만** 수행한다.

---

## Reference

- **공용 모듈:** `framework-ts/modules/devian-network-server/`
- **그룹 런타임:** `framework-ts/modules/devian-protocol-{group}/generated/ServerRuntime.g.ts`
- **정책 정본:** `skills/devian/03-ssot/SKILL.md`
