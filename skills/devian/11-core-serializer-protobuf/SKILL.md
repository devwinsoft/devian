# Devian v10 — Serializer / Protobuf Policy

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian v10에서 사용하는 직렬화 정책(Encoding/Decoding)을 정의한다.

이 문서는 **“무슨 포맷을 지원/금지하는가”**만 서술한다.
구체적인 바이트 레이아웃/런타임 API는 **`docs/generated/devian-reference.md`**를 정답으로 본다.

---

## Supported Formats (Policy)

### 1) JSON

- 디버깅/툴링을 위한 기본 교환 포맷
- Protocol/Contracts/NDJSON 데이터 모두 JSON 계열을 사용한다

### 2) Protobuf-style Wire (Devian)

- v10은 “.proto → protoc” 체인을 사용하지 않는다
- 대신 Protobuf wire 개념을 차용한 **Devian 전용 인코더/디코더**를 사용한다
- 목적: 작은 payload, 빠른 파싱, Span 기반 처리

---

## DFF (Data Field Format)

XLSX에서 `enum:*` / `class:*` 타입 셀에 들어가는 텍스트 표현을 **DFF**로 정의한다.

- DFF는 “셀 텍스트 → 런타임 오브젝트” 변환 규약이다
- 빌드 도구가 DFF를 항상 해석해야 한다고 강제하지 않는다
  - (예) 빌드 산출 NDJSON에 셀 원문을 보존하고, 런타임 로더에서 DFF를 해석할 수 있다

DFF 문법/예시는 `skills/devian/25-class-cell-format/SKILL.md`를 따른다.

---

## MUST / MUST NOT

MUST

- 인코딩/디코딩은 **결정적**이어야 한다(같은 입력 → 같은 바이트/같은 객체)
- Protocol의 opcode/tag 정책(SSOT)을 위반하면 실패해야 한다

MUST NOT

- .proto 파일을 입력 정본으로 취급하지 않는다
- “편의상 임의 타입 매핑/임의 필드 스킵” 같은 묵시적 처리 금지

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- DFF 규약: `skills/devian/25-class-cell-format/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`