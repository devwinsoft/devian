# Devian v10 — Network Server (TypeScript)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

TypeScript 기반 네트워크 서버 모듈의 설계 원칙과 책임 분리를 정의한다.

---

## 설계 원칙

### 1. 공용 모듈과 그룹별 런타임 분리

- **@devian/core**: 공용 코드만 포함
  - WebSocket transport (세션 관리, binary send)
  - Frame 파싱 (int32 LE opcode + payload)
  - Tagged BigInt JSON codec (옵션)
  - NetworkServer (runtime 주입)
  - NetworkClient (runtime 주입)

- **@devian/network-{group}**: 그룹별 런타임 제공
  - Inbound opcode 이름 조회
  - Inbound dispatch (stub.dispatch)
  - Outbound proxy 생성
  - Protobuf codec (기본)
  - Devian.Protocol.{Group} namespace 트리

### 2. Codec 정합

- **기본:** protobuf codec (생성된 Stub/Proxy의 기본 codec)
- **선택:** `createServerRuntime(customCodec)`로 custom codec 주입 가능
- codec 미주입 시 Stub/Proxy 각자의 기본 protobuf codec 사용

### 3. Unknown Opcode 정책

Unknown inbound opcode는:
- **절대 disconnect/close 하지 않는다**
- `onUnknownInboundOpcode` 훅으로 위임한다
- 기본 동작은 warn 로그만

---

## 디렉토리 구조

```
framework-ts/module/devian-core/
├── src/
│   ├── index.ts              # 모듈 exports
│   ├── net/
│   │   ├── shared/
│   │   │   ├── frame.ts      # 프레임 파싱/생성
│   │   │   └── codec.ts      # Tagged BigInt JSON codec
│   │   ├── transport/
│   │   │   ├── ITransport.ts # Transport 인터페이스
│   │   │   └── WsTransport.ts # WebSocket 구현
│   │   └── protocol/
│   │       ├── INetworkRuntime.ts   # 런타임 인터페이스
│   │       ├── NetworkServer.ts     # 서버측 메시지 처리
│   │       └── NetworkClient.ts     # 클라이언트측 메시지 처리
```

---

## 책임 분리

| 구성요소 | 책임 | 금지 |
|----------|------|------|
| WsTransport | 세션 관리, binary 송수신 | opcode 해석 |
| NetworkServer | frame 파싱, runtime 호출 | disconnect on unknown |
| NetworkClient | ws message → runtime dispatch | disconnect on unknown |
| INetworkRuntime | opcode 조회, dispatch, proxy 생성 | - |
| SampleServer | 조립 + 핸들러 등록 | 비즈니스 로직 확장 |

---

## 예제 앱

위치: `framework-ts/apps/SampleServer/`

예제 앱은 **조립 + 핸들러 등록만** 수행한다. SampleServer는 protobuf codec을 기본으로 사용한다.

```typescript
import { WsTransport, NetworkServer } from '@devian/core';
import { createServerRuntime, Sample2C } from '@devian/network-sample';

// codec 미주입 = protobuf 기본
const runtime = createServerRuntime();
const stub = runtime.getStub();

// 핸들러 등록
stub.onPing(async (sessionId, msg) => { ... });
stub.onEcho(async (sessionId, msg) => { ... });
```

---

## Reference

- **공용 모듈:** `framework-ts/module/devian-core/`
- **그룹 런타임:** `framework-ts/module-gen/devian-network-{group}/generated/ServerRuntime.g.ts`
- **정책 정본:** `skills/devian/03-ssot/SKILL.md`
