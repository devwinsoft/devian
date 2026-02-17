# 43-contractgen-implementation

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian-core/03-ssot/SKILL.md

## Purpose

DATA(DomainType=DATA)에서 사용하는 **contracts JSON 입력 규약**을 정의한다.

이 문서는 “contracts 입력 포맷과 경로”만 규정한다.
생성 코드의 네임스페이스/타입/시그니처는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Inputs

입력은 `{buildInputJson}`의 `domains` 섹션이 정본이다.

- `domains[{DomainKey}].contractDir`
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
    { "name": "id", "type": "int" },
    { "name": "name", "type": "string" },
    { "name": "userType", "type": "enum:UserType" }
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

Contract는 Domain 단위로 Table과 함께 **단일 파일에 통합** 생성된다.

- staging: `{tempDir}/{DomainKey}/cs/Generated/{DomainKey}.g.cs`, `{tempDir}/{DomainKey}/ts/Generated/{DomainKey}.g.ts`
- final: `{csConfig.generateDir}/Devian.Domain.{DomainKey}/Generated/{DomainKey}.g.cs`, `{tsConfig.generateDir}/devian-domain-{domainkey}/Generated/{DomainKey}.g.ts`

---

## Domain 모듈 의존성 (Hard Rule)

**C# DATA Domain 모듈 의존성:**
- `Devian.Domain.{DomainKey}.csproj`는 `..\Devian\Devian.csproj`만 ProjectReference 한다.
- Common 모듈을 포함한 모든 Domain 모듈이 동일한 규칙을 따른다.

**TS DATA Domain 패키지 의존성:**
- `@devian/module-{domainkey}`는 `@devian/core`만 의존한다.

---

## Failure Rules (MUST)

- JSON 파싱 실패 → 빌드 실패
- enum/class 이름 중복 등 명백한 충돌 → 빌드 실패

---

## Reference

- Policy SSOT: `skills/devian-core/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드