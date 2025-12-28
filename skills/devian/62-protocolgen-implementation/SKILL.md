# Devian – 62 Protocolgen Implementation

## Purpose

**중립 IDL(JSON)로부터 C#/TS 프로토콜 산출물(DTO, opcode, dispatcher skeleton용 타입)을 생성한다.**

**Phase 3 상태:**
- `.json` 입력: 지원됨
- `.proto` 입력: JSON(proto-json)으로 파싱 시도 → 성공 시 처리, 실패 시 "IDL 미지원(Phase 4)" 에러

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
| `{domains[domain].protocolsDir}/{protocolFile}` | Protocol Spec 원천 (단일 파일) |
| `build.json` | `domains[domain].protocolsDir` + `protocolFile` |

**v9 변경:** Protocol은 glob 패턴이 아닌 **단일 파일**로 지정됨.

### .proto 파일 처리 규칙

| 확장자 | 처리 |
|--------|------|
| `.json` | JSON으로 파싱 → ProtocolSpec |
| `.proto` | JSON 파싱 시도 → 성공 시 처리, 실패 시 에러 |

**.proto가 JSON이 아닌 경우 (Protobuf IDL):**
```
[domain='C2Game'] Protocol '.proto' is not JSON (proto-json). 
Protobuf IDL .proto is not supported yet (Phase 5). file='...'
```

> **Note:** Protobuf IDL .proto 파싱은 Phase 5에서 지원 예정.

### 입력 구조 (v9)

```
input/C2Game/protocols/        ← domains["C2Game"].protocolsDir
└── C2Game.proto               ← protocolFile (단일 파일)
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
{domains[domain].protocolsDir}/{filename}.json
```

- **protocolsDir = 도메인별 명시 경로**
- **파일명 = namespace/채널** (예: `ws.json` → namespace = `ws`)

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

## 구현 요약 (v6)

### v6 입력 경로

- `DomainConfig.protocolsDir: string`
- 프로토콜 파일 로딩 로직:
  ```
  {domains[domain].protocolsDir}/*.json 에서 파일 목록 결정
  ```
- 파일 로드 후:
  ```csharp
  spec.Namespace = Path.GetFileNameWithoutExtension(filePath);
  ```
- JSON `"namespace"` 필드가 있으면 **일치 검증만** 수행

### 출력 파일명 규칙

| 타겟 | 파일명 |
|------|--------|
| C# | `{namespace}.g.cs` (예: `ws.g.cs`) |
| TS | `{namespace}.g.ts` (예: `ws.g.ts`) |

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
| 2.0.0 | 2025-12-28 | **v9 스키마: protocolFile 단수, 섹션 배타 규칙 |
| 1.1.0 | 2025-12-28 | Phase 3: .proto(JSON) 지원, IDL은 Phase 4 |
| 1.0.0 | 2025-12-28 | Initial |
