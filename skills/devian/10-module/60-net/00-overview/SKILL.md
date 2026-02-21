# devian/10-module/60-net — Overview

Devian Net layer: transport adapters, WebSocket/HTTP-RPC 클라이언트, 서버, WebGL 브릿지.

Maps to: `framework-cs/module/Devian/src/Net/`, `framework-ts/module/devian/src/net/`

- **Transport Adapter**: WebSocket transport 계약
- **Dispatcher Skeleton**: opcode 기반 수신 라우팅
- **WS Client**: WebSocket 클라이언트 런타임 (Non-WebGL + WebGL polling)
- **HTTP RPC Client**: Binary→base64 단일 파라미터 POST RPC
- **Network Server**: TypeScript 네트워크 서버
- **Game Network Client**: TypeScript 게임 클라이언트 테스트 앱
- **WebGL Bridge**: .jslib ↔ C# polling 계약
- **WebGL Memory**: ptr/len/string 수명 규칙

---

## Start Here

| Document | Description |
|----------|-------------|
| [70-ws-transport-adapter](../70-ws-transport-adapter/SKILL.md) | Transport adapter contract |
| [71-ws-dispatcher-skeleton](../71-ws-dispatcher-skeleton/SKILL.md) | Dispatcher skeleton |
| [72-network-ws-client](../72-network-ws-client/SKILL.md) | WebSocket client runtime |
| [73-network-http-rpc-client](../73-network-http-rpc-client/SKILL.md) | HTTP RPC client |
| [74-network-server](../74-network-server/SKILL.md) | TS network server |
| [75-game-network-client](../75-game-network-client/SKILL.md) | TS game client |
| [76-webgl-ws-polling-bridge](../76-webgl-ws-polling-bridge/SKILL.md) | WebGL polling bridge |
| [77-webgl-jslib-memory-rules](../77-webgl-jslib-memory-rules/SKILL.md) | WebGL memory rules |

---

## Related

- [Parent Policy](../../01-policy/SKILL.md)
- [SSOT](../../03-ssot/SKILL.md)
- [Devian Index](../../../SKILL.md)
