# Example Network Server

Devian Network Server 예제 앱입니다.

## 구조

이 앱은 **조립만** 수행합니다:

1. `WsTransport` - WebSocket 전송 계층
2. `NetworkServerRuntime` - Game 그룹 프로토콜 런타임
3. `NetworkServer` - 프로토콜 서버 (transport + runtime 연결)

## 실행

```bash
npm install
npm start
```

## 핸들러

- `onLoginRequest` - 로그인 요청 처리
- `onJoinRoomRequest` - 방 입장 요청 처리
- `onChatMessage` - 채팅 메시지 브로드캐스트
- `onUploadData` - 데이터 업로드 로깅

## Unknown Opcode 정책

Unknown opcode는 **절대 disconnect 하지 않습니다**.
`onUnknownInboundOpcode` 훅으로 위임되며, 기본 동작은 로깅만 합니다.
