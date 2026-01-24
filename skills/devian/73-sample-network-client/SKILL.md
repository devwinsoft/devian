# Devian v10 — Sample Network Client (TypeScript)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

WebSocket 클라이언트 테스트 앱의 설계 원칙과 규칙을 정의한다.  
SampleServer와의 왕복 통신을 통해 Proxy/Stub + protobuf codec + frame 포맷이 정상인지 검증한다.

---

## 핵심 구조

```typescript
import { NetworkClient } from '@devian/core';
import { createClientRuntime } from '@devian/network-sample';

// 1. ClientRuntime 생성 (codec 미주입 = protobuf 기본)
const { runtime, sample2CStub, c2SampleProxyFactory } = createClientRuntime();

// 2. NetworkClient 생성
const client = new NetworkClient(runtime, { sessionId: 0 });

// 3. WebSocket 메시지 위임
ws.on('message', (raw) => client.onWsMessage(raw));

// 4. Inbound 핸들러 등록 (Sample2C)
sample2CStub.onPong((sid, msg) => { ... });
sample2CStub.onEchoReply((sid, msg) => { ... });

// 5. Outbound 전송 (C2Sample)
const c2sampleProxy = c2SampleProxyFactory(sendFn);
c2sampleProxy.sendPing(0, { ... });
c2sampleProxy.sendEcho(0, { ... });
```

---

## 고정 규칙

### 프레임 포맷

```
[int32 LE opcode][payload bytes]
```

- opcode: 4바이트 little-endian 정수
- payload: 가변 길이 바이트

### Codec 정합

- **기본:** protobuf codec (생성된 Stub/Proxy의 기본 codec)
- **선택:** `createClientRuntime(customCodec)`로 custom codec 주입 가능
- codec 미주입 시 Stub/Proxy 각자의 기본 protobuf codec 사용

### Unknown Opcode 처리

- 수신 opcode가 인식되지 않으면:
  - **절대 disconnect/close 하지 않는다**
  - 처리 우선순위:
    1. `NetworkClient` 옵션 핸들러 (`onUnknownInboundOpcode`)
    2. `runtime.setUnknownInboundOpcode(handler)`
    3. 기본 warn 로그

---

## 금지 사항

- `generated` 코드 임의 수정 금지 (생성기 변경과 동기화 필수)
- 샘플 앱이 아닌 build system 변경은 별도 phase에서만 수행
- unknown opcode에서 disconnect/close 호출 금지

---

## 디렉토리 구조

```
framework-ts/apps/SampleClient/
├── package.json      # 앱 정의
├── tsconfig.json     # TypeScript 설정
├── README.md         # 실행 방법
└── src/
    └── index.ts      # 메인 엔트리 (NetworkClient + ClientRuntime)
```

### 파일 역할

| 파일 | 역할 |
|------|------|
| `src/index.ts` | WebSocket 연결, NetworkClient로 메시지 처리, Stub/Proxy 사용 |

프레임 파싱 및 메시지 디스패치는 `NetworkClient`가 내부적으로 처리한다.

---

## Reference

- **서버 앱:** `framework-ts/apps/SampleServer/`
- **네트워크 모듈:** `framework-ts/module/devian-core/`
- **프로토콜:** `framework-ts/module-gen/devian-network-sample/`
