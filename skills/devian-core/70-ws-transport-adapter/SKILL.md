# 70-ws-transport-adapter

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian-core/03-ssot/SKILL.md

## Purpose

WebSocket 기반의 Transport Adapter가 지켜야 하는 **역할/경계/검증 포인트**를 정의한다.

이 문서는 WebSocket 구현을 강제하지 않는다.
또한 런타임 인터페이스/프레임 포맷/시그니처는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Responsibilities

1) **송신**
- Protocol generated 코드에서 만들어진 frame(bytes)을 네트워크로 전송

2) **수신**
- 네트워크에서 수신한 frame(bytes)을 “opcode 기반 라우팅”이 가능한 형태로 전달

3) **연결/세션 관리**
- 연결 수립/종료 이벤트
- sessionId(혹은 동등한 식별자) 매핑

---

## Must / Must Not

MUST

- frame(bytes)는 **바이트 단위로 보존**해야 한다(중간에서 재인코딩/재구성 금지)
- ping/pong, reconnect, backpressure 등은 adapter 책임 영역

MUST NOT

- Transport가 Protocol 타입(메시지 클래스)을 직접 참조/의존하지 않는다
- opcode/tag 레지스트리를 transport가 “임의로 재작성”하지 않는다

---

## Interop Notes

- "Protocol layer(sender/proxy)"와 "Network layer(transport)"의 경계는 Reference에 정의된 계약을 따른다.
- 동일 프로젝트 내에서 여러 프로토콜을 사용할 경우, `{buildInputJson}`의 targetDirs 설계로 산출 충돌을 회피해야 한다.

**WebGL 폴링 계약 (Hard Rule):**

WebGL(`UNITY_WEBGL && !UNITY_EDITOR`)에서 WebSocket Transport는 **콜백(SendMessage) 기반이 아니라 "폴링 기반 브릿지 계약"**을 따른다.

- 계약/메모리 규칙 정본:
  - [76-webgl-ws-polling-bridge](../76-webgl-ws-polling-bridge/SKILL.md)
  - [77-webgl-jslib-memory-rules](../77-webgl-jslib-memory-rules/SKILL.md)

---

## Reference

- Policy SSOT: `skills/devian-core/03-ssot/SKILL.md`
- Runtime layering: `skills/devian-core/10-core-runtime/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드