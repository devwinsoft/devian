# Devian v10 — ContractGen (Policy)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

DATA(DomainType=DATA)에서 사용하는 **contracts JSON 입력 규약**을 정의한다.

이 문서는 “contracts 입력 포맷과 경로”만 규정한다.
생성 코드의 네임스페이스/타입/시그니처는 **`docs/generated/devian-reference.md`**를 정답으로 본다.

---

## Inputs

입력은 build.json의 `domains` 섹션이 정본이다.

- `domains[{DomainKey}].contractsDir`
- `domains[{DomainKey}].contractFiles` (예: `["*.json"]`)

---

## Contract Spec JSON

Contract spec는 `enums`와 `classes`로 구성된다.

### Enum

```json
{
  "name": "UserType",
  "doc": "optional doc",
  "values": [
    { "name": "Guest", "value": 0 },
    { "name": "Member", "value": 1 }
  ]
}
```

### Class

```json
{
  "name": "UserProfile",
  "doc": "optional doc",
  "fields": [
    { "name": "userId", "type": "int64" },
    { "name": "displayName", "type": "string", "optional": true }
  ]
}
```

### Root

```json
{
  "enums": [ /* ... */ ],
  "classes": [ /* ... */ ]
}
```

> 지원 타입 문자열(예: `int64`, `string`, 배열 등)과 타입 매핑의 정답은 Reference를 따른다.

---

## Outputs & Paths

경로 규약은 SSOT를 따른다.

- staging: `{tempDir}/{DomainKey}/cs/generated/**`, `{tempDir}/{DomainKey}/ts/generated/**`
- final: `{csTargetDir}/generated/**`, `{tsTargetDir}/generated/**`

---

## Failure Rules (MUST)

- JSON 파싱 실패 → 빌드 실패
- enum/class 이름 중복 등 명백한 충돌 → 빌드 실패

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`