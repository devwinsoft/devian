# Devian v10 — Consumption Blueprint (Policy)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian 산출물을 실제 앱(클라/서버/툴)에서 소비하는 기본 청사진을 제공한다.

이 문서는 "어떤 조각을 어디에 연결한다"는 구조만 말한다.
구체적인 타입/시그니처/호출 예시는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## What You Get

### 1) DATA 산출물 (DomainKey 단위)

- contracts 생성물 (enum/class 타입)
- tables 생성물 (row 타입 / 컨테이너)
- ndjson 데이터 파일

### 2) PROTOCOL 산출물 (ProtocolName 단위)

- 메시지 타입
- codec(JSON + Protobuf-style)
- 발신 프록시(sender/proxy)
- 수신 확장 지점(handlers/stub 등)

---

## Typical Consumption Flow

### DATA

1) `{dataTargetDir}/{DomainKey}/ndjson/*.ndjson` 또는 `{dataTargetDir}/{DomainKey}/bin/*.asset` (ASSET 테이블만)를 로드한다.
2) generated 컨테이너/로더를 통해 테이블을 구성한다.
3) `enum:*` / `class:*` 셀 원문이 필요한 경우 DFF 규약으로 해석한다.

### PROTOCOL

1) transport adapter(WebSocket 등)를 준비한다.
2) 수신 bytes → opcode 라우팅 → decode → 유저 핸들러 호출 구조를 만든다.
3) 발신은 generated 프록시를 통해 "message → frame(bytes)"로 만든 뒤 transport로 보낸다.

---

## Must / Must Not

MUST

- input_common.json의 targetDirs 충돌을 피하도록 설계한다(클린-카피로 인한 덮어쓰기 방지)
- opcode/tag 결정성을 깨뜨리는 임의 변경을 금지한다

MUST NOT

- generated 코드에 직접 수정을 가하지 않는다(재생성 시 소실)

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Transport: `skills/devian/70-ws-transport-adapter/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
