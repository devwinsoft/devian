# Sample Server

Devian Network Server 샘플 앱입니다. 현재 샘플은 Ping/Echo 기반입니다.

## 구조

이 앱은 **조립만** 수행합니다:

1. `WsTransport` - WebSocket 전송 계층
2. `NetworkServerRuntime` - Sample 그룹 프로토콜 런타임 (protobuf codec 기본)
3. `NetworkServer` - 프로토콜 서버 (transport + runtime 연결)

## 실행

```bash
npm install
npm start
```

## 핸들러

- `onPing` - Ping 요청 처리 → Pong 응답
- `onEcho` - Echo 요청 처리 → EchoReply 응답

## Unknown Opcode 정책

Unknown opcode는 **절대 disconnect 하지 않습니다**.
`onUnknownInboundOpcode` 훅으로 위임되며, 기본 동작은 로깅만 합니다.
