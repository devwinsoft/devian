# Devian v10 — Example Network Client (TypeScript)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

WebSocket 클라이언트 테스트 앱의 설계 원칙과 규칙을 정의한다.  
ExampleNetworkServer와의 왕복 통신을 통해 Proxy/Stub + codec + frame 포맷이 정상인지 검증한다.

---

## 고정 규칙

### 프레임 포맷

```
[int32 LE opcode][payload bytes]
```

- opcode: 4바이트 little-endian 정수
- payload: 가변 길이 바이트

### Codec 정합

- 클라이언트는 `@devian/network-server`의 `defaultCodec`을 사용한다
- Proxy/Stub에 동일 codec을 주입한다

### Proxy/Stub 사용

- 송신: `C2Game.Proxy(sendFn, defaultCodec)` 사용
- 수신: `Game2C.Stub(defaultCodec)` + `dispatch(0, opcode, payload)`

### Unknown Opcode 처리

- 수신 opcode가 `Game2C.getOpcodeName(opcode)`로 조회되지 않으면:
  - **로그만 남기고 무시**
  - **절대 disconnect/close 하지 않는다**

---

## 금지 사항

- `generated` 코드 수정 금지
- `builder` 수정 금지
- unknown opcode에서 disconnect/close 호출 금지

---

## 디렉토리 구조

```
framework-ts/apps/ExampleNetworkClient/
├── package.json      # 앱 정의
├── tsconfig.json     # TypeScript 설정
├── README.md         # 실행 방법
└── src/
    ├── index.ts      # 메인 엔트리 (연결, 송수신)
    └── frame.ts      # 프레임 파싱 유틸리티
```

### 파일 역할

| 파일 | 역할 |
|------|------|
| `src/index.ts` | WebSocket 연결, Proxy로 송신, Stub으로 수신 처리 |
| `src/frame.ts` | 수신 바이트에서 opcode/payload 분리 |

---

## Reference

- **서버 앱:** `framework-ts/apps/ExampleNetworkServer/`
- **네트워크 모듈:** `framework-ts/modules/devian-network-server/`
- **프로토콜:** `framework-ts/modules/devian-protocol-client/`
