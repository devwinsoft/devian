# Devian v10 — Protocol Codegen (Overview)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

PROTOCOL(DomainType=PROTOCOL) 입력으로부터 C#/TS 프로토콜 코드를 생성하는 **전체 흐름**을 정의한다.

이 문서는 **입력 포맷 / 레지스트리(결정성) / 경로 규약**만 규정한다.
생성 코드의 구체적 API/산출물은 **`docs/generated/devian-reference.md`**를 정답으로 본다.

---

## Inputs

입력은 `input/build/build.json`의 `protocols` 섹션이 정본이다.

- `protocols[{LinkName}].protocolsDir`
- `protocols[{LinkName}].protocolFile` (JSON)

### Protocol Spec JSON (필수 필드)

최소 구조:

```json
{
  "direction": "client_to_server | server_to_client | bidirectional",
  "messages": [
    {
      "name": "MessageName",
      "opcode": 100,              // optional
      "fields": [
        { "name": "field", "type": "int32", "tag": 1, "optional": true }
      ]
    }
  ]
}
```

추가 키가 존재할 수 있다. “지원 여부/정확한 스키마”는 Reference를 정답으로 본다.

### JSON `namespace` 검증

JSON에 `namespace`가 있는 경우:

- **반드시 ProtocolName(파일명 base)과 일치**해야 한다.
- 불일치 시 빌드 실패.

---

## Determinism Gate (opcode / tag)

Protocol 호환성을 위해 Registry 파일을 사용한다.

- `{ProtocolName}.opcodes.json`
- `{ProtocolName}.tags.json`

정책:

1) 명시 값 우선
2) 레지스트리 값은 호환성 보존을 위해 유지
3) 미지정 값은 **결정적 규칙으로 자동 할당**
4) Tag의 reserved range(19000..19999) 금지

> 자동 할당의 상세 규칙(최소값/정렬/증가)은 구현/Reference를 정답으로 본다.

---

## Outputs & Paths

경로 규약은 SSOT를 따른다.

- staging: `{tempDir}/{LinkName}/cs/generated/**`, `{tempDir}/{LinkName}/ts/generated/**`
- final: `{csTargetDir}/generated/**`, `{tsTargetDir}/generated/**`

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`
