# Devian – 62 Protocolgen Implementation

## Purpose

**중립 IDL(JSON)로부터 C#/TS 프로토콜 산출물(DTO, opcode, dispatcher skeleton용 타입)을 생성한다.**

이 스킬은 `build.json`을 읽고 실제 코드를 생성하는 **구현 스펙**이다.

---

## Belongs To

**Consumer / Tooling (Build tool)**

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Protocol Spec(JSON) 스키마 정의 | messages, opcode, fields |
| opcode / message id 관리 규칙 | 결정적 할당 (파일 단위) |
| C# DTO 생성 | `tempDir/{domain}/cs/{namespace}.g.cs` |
| TS DTO 생성 | `tempDir/{domain}/ts/{namespace}.g.ts` |

### Out of Scope

| 항목 | 설명 |
|------|------|
| 실제 네트워크 전송/서버 구조 | ❌ consumer 영역 |
| NestJS/Unity 아키텍처 강제 | ❌ consumer 영역 |
| 런타임 디스패처 구현 | ❌ 41은 blueprint |
| Contract Spec (types) 처리 | ❌ 64 스킬 |

---

## Inputs

| Input | 설명 |
|-------|------|
| `{protocolsDir}/{domain}/{filename}.json` | Protocol Spec 원천 |
| `build.json` | `inputDirs.protocolsDir` |
| `build.json` | `domains[domain].protocolFiles` (파일 목록 또는 glob) |

### 입력 구조 (확정)

```
input/protocols/
└── net/                    ← domain = "net"
    ├── C2Game.json         ← namespace = "C2Game"
    ├── Game2C.json         ← namespace = "Game2C"
    └── S2S.json            ← namespace = "S2S"
```

---

## Outputs

| 타겟 | 생성 경로 |
|------|----------|
| C# | `{tempDir}/{domain}/cs/{namespace}.g.cs` |
| TS | `{tempDir}/{domain}/ts/{namespace}.g.ts` |

> **모든 생성은 tempDir에서** — 복사는 `csTargetDirs`, `tsTargetDirs`로

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | **폴더 이름 = domain** |
| 2 | **파일 이름 = namespace** — `C2Game.json` → namespace = `C2Game` |
| 3 | JSON 내부 `"namespace"`는 **선택적** — 있으면 파일명과 일치 검증, 불일치 시 빌드 실패 |
| 4 | **생성은 tempDir/{domain}/{cs\|ts}/에서만** |
| 5 | opcode는 **결정적**이어야 한다 (명시 우선, 없으면 정렬 기반 자동 할당) |
| 6 | opcode 할당 단위는 **파일 단위** — 파일 간 opcode 공간 미공유 |
| 7 | core runtime은 **generated에 의존하지 않는다** (consumer가 import) |

---

## 절대 금지 사항 (MUST NOT)

| # | 금지 항목 |
|---|----------|
| 1 | protocol namespace를 폴더 기준으로 해석 |
| 2 | `protocolNamespaces` 사용 (폐기됨) |
| 3 | domain 외부 경로 참조 (`../`, `net/*.json` 등) |
| 4 | 1개 파일에서 여러 protocol namespace 정의 |
| 5 | primary output 경로를 JSON에서 읽기 |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | opcode 충돌 시 명확한 에러 메시지 |
| 2 | 생성 파일 상단에 `DO NOT EDIT` 헤더 |

---

## Protocol Spec JSON Schema (확정)

### 파일 위치

```
{protocolsDir}/{domain}/{filename}.json
```

- **폴더명 = domain** (예: `net`, `chat`, `lobby`)
- **파일명 = namespace/채널** (예: `C2Game.json` → namespace = `C2Game`)

### 스키마

```json
{
  "direction": "client_to_server",
  "messages": [
    {
      "name": "Login",
      "opcode": 1,
      "fields": [
        { "name": "token", "type": "string" }
      ]
    },
    {
      "name": "Ping",
      "fields": [
        { "name": "time", "type": "int64" }
      ]
    }
  ]
}
```

### 필드 설명

| 필드 | 필수 | 설명 |
|------|------|------|
| `namespace` | ❌ | **선택적** — 없으면 파일명 사용, 있으면 파일명과 일치 검증 |
| `direction` | ❌ | `client_to_server`, `server_to_client`, `bidirectional` (기본값) |
| `messages` | ✅ | 메시지 배열 |
| `messages[].name` | ✅ | 메시지 이름 |
| `messages[].opcode` | ❌ | 명시 opcode (없으면 자동 할당) |
| `messages[].fields` | ✅ | 필드 배열 |
| `messages[].fields[].name` | ✅ | 필드 이름 |
| `messages[].fields[].type` | ✅ | 필드 타입 |
| `messages[].fields[].optional` | ❌ | nullable 여부 |

### namespace 결정 규칙 (핵심)

```
1. filenameBase = Path.GetFileNameWithoutExtension(filePath)
2. JSON에 "namespace" 필드가 있으면:
   - filenameBase와 비교
   - 불일치 시 빌드 실패
3. spec.Namespace = filenameBase (강제 적용)
```

---

## Type Mapping

| IDL Type | C# | TypeScript |
|----------|-----|------------|
| `string` | `string` | `string` |
| `int32` | `int` | `number` |
| `int64` | `long` | `number` |
| `float` | `float` | `number` |
| `double` | `double` | `number` |
| `bool` | `bool` | `boolean` |
| `T[]` | `T[]` | `T[]` |
| `map<string,T>` | `Dictionary<string,T>` | `Record<string,T>` |
| `ref:SomeType` | 같은 namespace 또는 fully qualified | 같은 방식 |

---

## Opcode Algorithm

```
1. 파일 단위로 message를 수집 (namespace = 파일명)
2. opcode가 명시된 항목은 예약
3. 나머지는 name 오름차순 정렬 후 빈 opcode를 낮은 번호부터 할당
4. 충돌 시 에러
```

> **Note:** opcode 할당은 **파일 단위**이다.  
> `C2Game.json`과 `Game2C.json`은 opcode 공간을 공유하지 않는다.

---

## 구현 변경 사항 (필수)

### 1. 폐기

- `DomainConfig.ProtocolNamespaces`
- `"namespace = 폴더"` 개념

### 2. 도입/변경

- `DomainConfig.protocolFiles: string[]`
- 프로토콜 파일 로딩 로직:
  ```
  {protocolsDir}/{domain}/ 하위에서 protocolFiles로 파일 목록 결정
  ```
- 파일 로드 후:
  ```csharp
  spec.Namespace = Path.GetFileNameWithoutExtension(filePath);
  ```
- JSON `"namespace"` 필드가 있으면 **일치 검증만** 수행

### 3. 출력 파일명 규칙

| 타겟 | 파일명 |
|------|--------|
| C# | `{namespace}.g.cs` (예: `C2Game.g.cs`) |
| TS | `{namespace}.g.ts` (예: `C2Game.g.ts`) |

---

## Responsibilities

1. **Protocol Spec(JSON) → C#/TS DTO 생성 알고리즘 구현**
2. **namespace = 파일명 규칙 적용**
3. **opcode 결정적 할당 보장 (파일 단위)**
4. **tempDir/{domain}에 생성**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Protocol Spec 1개로 C#/TS DTO 파일 생성 |
| 2 | **namespace는 파일명에서 결정**된다 |
| 3 | JSON 내부 `"namespace"` 불일치 시 빌드 **실패** |
| 4 | opcode 충돌/중복 시 빌드 **실패 + 원인 출력** |
| 5 | 생성은 `tempDir/{domain}/{cs\|ts}/`에서만 이루어진다 |
| 6 | `csTargetDirs`/`tsTargetDirs`가 비어있으면 복사 생략 |
| 7 | `C2Game`/`Game2C` 같은 다수 채널 파일이 정상 처리됨 |

---

## 리스크/검증 포인트

| 항목 | 검증 내용 |
|------|----------|
| enum/class name 충돌 | 파일명 기반 namespace로 TS enum/class name 충돌 여부 확인 |
| JSON `"namespace"` 제거 | 필드 없을 때 파서/생성기 정상 동작 |
| opcode 결정성 | 다수 채널 파일에서 opcode 할당 안정성 |
| 출력 파일명 | `{namespace}.g.cs`, `{namespace}.g.ts` 형식 준수 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `63-build-runner` | 빌드 실행기 |
| `64-contractgen-implementation` | Contract Spec 처리 |
| `41-ws-dispatcher-skeleton` | 참고 소비 패턴 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.4.0 | 2024-12-25 | **build.json 전면 재설계 반영**: tempDir 생성, targetDirs 복사 |
| 0.3.0 | 2024-12-25 | 입력 규칙 변경: 폴더=domain, 파일명=namespace |
| 0.2.0 | 2024-12-21 | Protocol Spec 확정 스키마 반영 |
| 0.1.0 | 2024-12-21 | Initial skill definition |
