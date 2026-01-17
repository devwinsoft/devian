# Sample Client

Devian Network Client 테스트 앱입니다.  
SampleServer와의 왕복 통신을 통해 Proxy/Stub + defaultCodec + frame 포맷을 검증합니다.

## 실행 순서

### 1. 서버 실행

```bash
cd framework-ts/apps/SampleServer
npm install
npm start
```

### 2. 클라이언트 실행 (다른 터미널)

```bash
cd framework-ts/apps/SampleClient
npm install
npm run dev
```

## 환경 변수

| 변수 | 기본값 | 설명 |
|------|--------|------|
| `WS_URL` | `ws://localhost:8080` | 서버 주소 |
| `USER_ID` | `user1` | 로그인 사용자 ID |
| `VERSION` | `1` | 클라이언트 버전 |

```bash
WS_URL=ws://127.0.0.1:9000 npm run dev
```

## 기대 로그

### 클라이언트
```
[Client] Connecting to ws://localhost:8080...
[Client] Connected to server
[Client] Sending LoginRequest...
[Handler] LoginResponse: { success: true, playerId: '1000', ... }
[Client] Sending ChatMessage...
[Handler] ChatNotify: { channel: 0, message: 'Hello from client!', ... }
```

### 서버
```
[App] Session 1 connected
[Handler] LoginRequest from session 1: ...
[Handler] ChatMessage from session 1: ...
```

## Unknown Opcode 처리

알 수 없는 opcode를 수신하면 로그만 남기고 무시합니다.  
**절대 disconnect 하지 않습니다.**

```
[Client] Unknown opcode 9999 (10 bytes) - ignoring
```
