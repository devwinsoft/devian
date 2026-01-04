# Devian v10 — Protocol Codegen (Overview)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

PROTOCOL(DomainType=PROTOCOL) 입력으로부터 C#/TS 프로토콜 코드를 생성하는 **전체 흐름**을 정의한다.

이 문서는 **입력 포맷 / 레지스트리(결정성) / 경로 규약**만 규정한다.
생성 코드의 구체적 API/산출물은 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Inputs

입력은 `build.json`의 `protocols` 섹션(배열)이 정본이다.

> build.json 위치는 유동적이다. 현재 프로젝트에서는 `input/build.json`에 위치한다.

```json
"protocols": [
  {
    "group": "Client",
    "protocolDir": "./Protocols/Client",
    "protocolFiles": ["C2Game.json", "Game2C.json"],
    "csTargetDir": "../framework/cs",
    "tsTargetDir": "../framework/ts"
  }
]
```

- `group`: ProtocolGroup 이름 (C# 프로젝트명, TS 폴더명에 사용)
- `protocolDir`: Protocol JSON 및 Registry 파일이 위치한 디렉토리
- `protocolFiles`: 처리할 Protocol JSON 파일 목록
- 파일명 base가 **ProtocolName**이 된다. (예: `C2Game.json` → `C2Game`)

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

추가 키가 존재할 수 있다. "지원 여부/정확한 스키마"는 코드를 정답으로 본다.

---

## Determinism Gate (opcode / tag)

Protocol 호환성을 위해 Registry 파일을 사용한다.

- `{ProtocolName}.opcodes.json`
- `{ProtocolName}.tags.json`

Registry 파일은 `protocolDir/generated/`에 위치하며, 빌드 시 갱신된다.
Registry는 "생성된 입력" 파일로, 기계가 생성하지만 입력 폴더에 보존된다.

정책:

1) 명시 값 우선
2) 레지스트리 값은 호환성 보존을 위해 유지
3) 미지정 값은 **결정적 규칙으로 자동 할당**
4) Tag의 reserved range(19000..19999) 금지

> 자동 할당의 상세 규칙(최소값/정렬/증가)은 코드를 정답으로 본다.

---

## Outputs & Paths

경로 규약은 SSOT를 따른다.

**C#:**
- staging: `{tempDir}/Devian.Network.{ProtocolGroup}/{ProtocolName}.g.cs`
- final: `{csTargetDir}/Devian.Network.{ProtocolGroup}/{ProtocolName}.g.cs`
- 프로젝트 파일: `Devian.Network.{ProtocolGroup}.csproj` (netstandard2.1)

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/{ProtocolName}.g.ts`, `index.ts`
- final: `{tsTargetDir}/{ProtocolGroup}/{ProtocolName}.g.ts`, `index.ts`

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
