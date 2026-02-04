# 31-example-network-client — Game Protocol TS 클라이언트 예제

Status: ACTIVE
AppliesTo: v10
Type: GUIDE

---

## Scope / Purpose

Game protocol 기반 TypeScript **클라이언트** 예제.

Devian 네트워크 클라이언트 조립 방식과 ClientRuntime 사용법을 보여준다.

---

## 경로 SSOT

| 구분 | 경로 |
|------|------|
| **Client** | `framework-ts/apps/GameClient/` |

---

## 핵심 조립 포인트

```typescript
import WebSocket from 'ws';
import { NetworkClient } from '@devian/core';
import { createClientRuntime } from '@devian/protocol-game/client-runtime';

// 1. Runtime 생성 (stub + proxy factory 반환)
const { runtime, game2CStub, c2GameProxyFactory } = createClientRuntime();

// 2. Unknown opcode 핸들러 등록 (선택)
runtime.setUnknownInboundOpcode(async (e) => {
    console.warn(`Unknown opcode=${e.opcode}`);
});

// 3. WebSocket 연결
const ws = new WebSocket(WS_URL);

// 4. NetworkClient 생성
const client = new NetworkClient(runtime, {
    sessionId: 0,
    onError: (err) => console.error(err),
});

// 5. Inbound 핸들러 등록 (Game2C)
game2CStub.onPong((sessionId, msg) => { ... });

// 6. Outbound Proxy 생성 (C2Game)
const sendFn = (sessionId: number, frame: Uint8Array) => ws.send(frame);
const c2GameProxy = c2GameProxyFactory(sendFn);

// 7. 메시지 수신 위임
ws.on('message', (raw) => client.onWsMessage(raw));

// 8. 메시지 전송
c2GameProxy.sendPing(0, { timestamp: BigInt(Date.now()), payload: 'hello' });
```

---

## 코덱 전환 (USE_JSON)

```typescript
import { defaultCodec as jsonCodec } from '@devian/core';

// false = Protobuf (default), true = Json
const USE_JSON = false;

const { runtime, ... } = USE_JSON ? createClientRuntime(jsonCodec) : createClientRuntime();
```

- **기본값**: Protobuf (바이너리, 성능 우선)
- **Json 옵션**: 디버깅/테스트용

---

## 실행 방법

```bash
# 서버가 실행 중이어야 함
npm -w GameClient run start
```

---

## 설정

| 항목 | 기본값 | 비고 |
|------|--------|------|
| URL | `ws://localhost:8080` | `WS_URL` 환경변수로 변경 가능 |
| Unknown opcode | 로깅만 | `runtime.setUnknownInboundOpcode` 훅 |

---

## Related

- [30-example-network-server](../30-example-network-server/SKILL.md) — Game Protocol TS 서버 예제
- [03-ssot](../03-ssot/SKILL.md) — Example Apps SSOT
- [20-example-protocol-game](../20-example-protocol-game/SKILL.md) — Game Protocol 예제
- [Protocol SSOT](../../devian-protocol/03-ssot/SKILL.md) — Opcode/Tag, Protocol UPM
