# Sample Client

Devian Network Client 테스트 앱입니다. 현재 샘플은 Ping/Echo 기반입니다.  
GameServer와의 왕복 통신을 통해 Proxy/Stub + protobuf codec + frame 포맷을 검증합니다.

## 실행 순서

### 1. 서버 실행

```bash
cd framework-ts/apps/GameServer
npm install
npm start
```

### 2. 클라이언트 실행 (다른 터미널)

```bash
cd framework-ts/apps/GameClient
npm install
npm run dev
```

## 환경 변수

| 변수 | 기본값 | 설명 |
|------|--------|------|
| `WS_URL` | `ws://localhost:8080` | 서버 주소 |

```bash
WS_URL=ws://127.0.0.1:9000 npm run dev
```

## 기대 로그

### 클라이언트
```
[GameClient] Connecting to ws://localhost:8080...
[GameClient] Connected to server
[GameClient] Sending Ping...
[Handler] Pong: { timestamp: '...', serverTime: '...' }
[GameClient] Sending Echo...
[Handler] EchoReply: { message: 'echo from client', echoedAt: '...' }
```

### 서버
```
[GameServer] Session 1 connected
[Handler] Ping from session 1: ...
[Handler] Echo from session 1: ...
```

## Unknown Opcode 처리

알 수 없는 opcode를 수신하면 로그만 남기고 무시합니다.  
**절대 disconnect 하지 않습니다.**

```
[GameClient] Unknown opcode 9999 (10 bytes) - ignoring
```
