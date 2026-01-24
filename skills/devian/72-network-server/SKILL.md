# Devian v10 — Network Server (TypeScript)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

TypeScript 기반 네트워크 서버 모듈의 설계 원칙과 책임 분리를 정의한다.

---

## Import 정본 (Hard Rule)

**Server 샘플/런타임 import 정본은 `@devian/network-sample/server-runtime` 이다.**

```typescript
// ✅ 정본 (MUST)
import { createServerRuntime, Sample2C } from '@devian/network-sample/server-runtime';
```

**루트 import 금지 (Hard Rule):**

```typescript
// ❌ 금지 - 루트에서 server runtime 가져오기
import { createServerRuntime } from '@devian/network-sample';  // FAIL
```

- `@devian/network-sample` 루트 import로 server runtime을 가져오는 것은 **금지**
- 루트 re-export가 코드에 남아있더라도, **문서/샘플에서는 사용 금지**
- 정본은 **반드시** `@devian/network-sample/server-runtime` 서브패스 사용

**이유:**
- Server/Client runtime 분리 명확화
- 번들 사이즈 최적화 (tree-shaking)
- 의존성 명시적 표현

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

TS SampleServer의 기본 codec은 Protobuf이다(인자 미주입).  
Json은 `@devian/core`의 `defaultCodec`를 runtime 생성 시 주입해서 선택한다.

```typescript
import { defaultCodec as jsonCodec } from '@devian/core';
import { createServerRuntime } from '@devian/network-sample/server-runtime';

// 기본(Protobuf)
const serverA = createServerRuntime();

// Json 선택
const serverB = createServerRuntime(jsonCodec);
```

### 3. Unknown Opcode 정책

Unknown inbound opcode는:
- **절대 disconnect/close 하지 않는다**
- `onUnknownInboundOpcode` 훅으로 위임한다
- 기본 동작은 warn 로그만

---

## 디렉토리 구조

```
framework-ts/module/devian/
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

예제 앱은 **조립 + 핸들러 등록만** 수행한다.

```typescript
import { WsTransport, NetworkServer } from '@devian/core';
import { createServerRuntime, Sample2C } from '@devian/network-sample/server-runtime';

// codec 미주입 = protobuf 기본
const runtime = createServerRuntime();
const stub = runtime.getStub();

// 핸들러 등록
stub.onPing(async (sessionId, msg) => { ... });
stub.onEcho(async (sessionId, msg) => { ... });
```

---

## Reference

- **공용 모듈:** `framework-ts/module/devian/`
- **그룹 런타임:** `framework-ts/module-gen/devian-network-{group}/generated/ServerRuntime.g.ts`
- **정책 정본:** `skills/devian/03-ssot/SKILL.md`
