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

입력은 `input_common.json`의 `protocols` 섹션(배열)이 정본이다.

> input_common.json 위치는 유동적이다. 현재 프로젝트에서는 `input/input_common.json`에 위치한다.

```json
"protocols": [
  {
    "group": "Game",
    "protocolDir": "./Protocols/Game",
    "protocolFiles": ["C2Game.json", "Game2C.json"]
  }
]
```

- `group`: ProtocolGroup 이름 (C# 프로젝트명, TS 폴더명에 사용)
- `protocolDir`: Protocol JSON 및 Registry 파일이 위치한 디렉토리
- `protocolFiles`: 처리할 Protocol JSON 파일 목록
- 파일명 base가 **ProtocolName**이 된다. (예: `C2Game.json` → `C2Game`)

**금지 필드 (존재 시 빌드 실패):**
- `csTargetDir` — `csConfig.generateDir` 사용
- `tsTargetDir` — `tsConfig.generateDir` 사용
- `upmName` — 자동 계산 (`com.devian.protocol.{group.toLowerCase()}`)

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
- staging: `{tempDir}/Devian.Protocol.{ProtocolName}/{ProtocolName}.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolName}/{ProtocolName}.g.cs`
- 프로젝트 파일: `Devian.Protocol.{ProtocolName}.csproj` (netstandard2.1)
- namespace: `Devian.Protocol.{ProtocolName}` (변경 금지)

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/{ProtocolName}.g.ts`, `index.ts`
- final: `{tsConfig.generateDir}/devian-network-{protocolgroup}/{ProtocolName}.g.ts`, `index.ts`
- 패키지명: `@devian/network-{protocolgroup}`

> **생성물 namespace 고정 (Hard Rule):**
> C# 생성물 namespace는 `Devian.Protocol.{ProtocolName}`으로 고정이며, 런타임 모듈 단일화와 무관하게 변경하지 않는다.

---

## UPM 산출물 정책 (Hard Rule)

**Protocol UPM(`com.devian.protocol.*`)은 Runtime-only로 생성한다.**

| 생성 대상 | 생성 여부 |
|-----------|----------|
| `Runtime/Devian.Protocol.{Group}.asmdef` | ✅ 생성 |
| `Runtime/{ProtocolName}.g.cs` | ✅ 생성 |
| `Editor/` 폴더 | ❌ 생성 금지 |
| `Devian.Protocol.{Group}.Editor.asmdef` | ❌ 생성 금지 |

**Runtime asmdef references 정책:**
- `Devian.Core`
- `Devian.Module.Common`

> SSOT: `skills/devian/03-ssot/SKILL.md` — Protocol UPM 자동 생성 규칙

---

## Unity Compatibility (Hard Rule)

Unity 환경에서의 호환성을 위해 다음 규칙을 강제한다.

**C# Protocol 코드 생성 시:**

1. **System.Text.Json 사용 금지**
   - Unity는 `System.Text.Json`을 기본 제공하지 않음
   - `using System.Text.Json;` 생성 금지
   - `JsonSerializer`, `JsonSerializerOptions` 등 사용 금지

2. **CodecJson 생성 금지**
   - JSON 코덱은 생성하지 않음
   - `CodecProtobuf`만 생성 (기본 코덱)
   - Stub 생성자 기본값도 `CodecProtobuf` 사용

3. **ICodec 인터페이스**
   - 인터페이스는 유지 (확장성)
   - 기본 구현체는 `CodecProtobuf`만 제공

---

## Common Module Dependency (Hard Rule)

Protocol 생성기는 **Common 참조 여부를 판정하지 않는다.**

Devian v10에서 생성되는 모든 PROTOCOL 모듈은 Common 모듈을 **무조건** 참조한다.

- 예외: Common 모듈 자기 자신은 제외(Protocol 모듈이 아니므로 해당 없음).

필수 적용:

- C#:
  - `Devian.Protocol.{ProtocolName}.csproj`는 `Devian + .Module.Common`을 `ProjectReference`로 포함해야 한다. (프로젝트 참조)
  - 각 생성물(`{ProtocolName}.g.cs`)은 `using Devian;`을 포함해야 한다. (namespace는 Devian 단일)
- TypeScript:
  - `@devian/network-{protocolgroup}`의 `package.json` `dependencies`에 `@devian/module-common`을 포함해야 한다.
- **Unity UPM:**
  - Protocol용 `.asmdef` 파일의 `references`에 `Devian + .Module.Common` 포함 필수
  - 예: `Devian.Protocol.Sample.asmdef" → "references": [..., "Devian + .Module.Common""]`

---

## TypeScript Namespace 규칙

TS 생성물은 **ProtocolName 단위**로 네임스페이스가 생성된다.

**생성 형태:**
```typescript
// {ProtocolName}.g.ts
export namespace {ProtocolName} {
    export interface MessageName { ... }
    export const Opcodes = { ... } as const;
}
```

**핵심 규칙:**
1. `.g.ts` 파일은 `export namespace {ProtocolName}`만 생성
2. `index.ts`에서 Direct export 제공
3. 소비자 코드는 Direct import를 사용

**생성 예시 (index.ts):**
```typescript
import * as C2GameMod from './C2Game.g';
import * as Game2CMod from './Game2C.g';

export const C2Game = C2GameMod.C2Game;
export const Game2C = Game2CMod.Game2C;

export { createServerRuntime } from './generated/ServerRuntime.g';
export { createClientRuntime } from './generated/ClientRuntime.g';
```

**사용법 (권장):**
```typescript
import { C2Game, Game2C, createClientRuntime } from '@devian/network-game';

// 타입 사용
const req: C2Game.LoginRequest = { ... };
const ack: Game2C.LoginAck = { ... };

// Opcode 사용
const opcode = C2Game.Opcodes.LoginRequest;
```

---

## ServerRuntime / ClientRuntime 생성 (TypeScript)

Protocol 그룹에 inbound와 outbound가 **정확히 1개씩** 존재하면 Runtime을 자동 생성한다.

**ServerRuntime (서버 관점):**
- inbound: client_to_server (예: C2Game)
- outbound: server_to_client (예: Game2C)

**ClientRuntime (클라이언트 관점):**
- inbound: server_to_client (예: Game2C)
- outbound: client_to_server (예: C2Game)

**생성 조건:**
- inbound 1개 + outbound 1개 → 생성
- bidirectional만 존재 → 생성 안함 (정상)
- 그 외 (0개, 2개 이상, 한쪽만 존재) → **빌드 에러**

**생성 파일:**
- `{tsConfig.generateDir}/devian-network-{group}/generated/ServerRuntime.g.ts`
- `{tsConfig.generateDir}/devian-network-{group}/generated/ClientRuntime.g.ts`

---

## TypeScript package.json (생성 산출물)

`devian-network-*` 패키지의 `package.json`은 **빌드 시스템이 생성하는 산출물**이다.

**수정 금지 정책:**
- 수동 편집 금지
- 빌드 시 덮어쓰기됨

**생성 내용:**
- `name`: `@devian/network-{group}`
- `exports`: `.` + Runtime 존재 시 `./server-runtime`, `./client-runtime`
- `dependencies`: `@devian/core`

> 위 dependencies 목록에는 **항상** `@devian/module-common`이 포함되어야 한다. (참조 판정 없음)

---

## Implementation Reference (정본 위치)

**구현 정본 파일:**

| 파일 | 함수 | 역할 |
|------|------|------|
| `framework-ts/tools/builder/build.js` | `generateCsproj(...)` | C# csproj 생성/보정 (ProtocolGroup 포함) |
| `framework-ts/tools/builder/build.js` | `ensureProtocolPackageJson(...)` | TS package.json 생성/보정 |
| `framework-ts/tools/builder/generators/protocol-cs.js` | `generateCSharpProtocol(...)` | C# `{ProtocolName}.g.cs` 생성 |

**Common 의존성 Hard Rule이 실제로 강제되는 지점:**

- **C#:**
  - csproj: `generateCsproj(...)`가 `Devian + .Module.Common` ProjectReference 포함
  - g.cs: `generateCSharpProtocol(...)`가 `using Devian;` 포함
- **TypeScript:**
  - package.json: `ensureProtocolPackageJson(...)`가 dependencies에 `@devian/module-common` 포함

---

## Verification Checklist (Hard)

빌드 후 반드시 확인해야 하는 사항:

**C#:**

1. 생성된 `framework-cs/module/Devian.Protocol.{ProtocolName}/Devian.Protocol.{ProtocolName}.csproj`에  
   `..\..\module\` + `Devian` + `.Module.Common` + `\` + `Devian` + `.Module.Common.csproj` ProjectReference 존재

2. 생성된 `framework-cs/module/Devian.Protocol.{ProtocolName}/{ProtocolName}.g.cs` 상단에  
   `using Devian;` 존재

3. 생성된 `{ProtocolName}.g.cs`에 `System.Text.Json` 관련 코드 없음

4. 생성된 `{ProtocolName}.g.cs`에 `CodecJson` 클래스 없음

**TypeScript:**

5. 생성된 `framework-ts/module/devian-network-{group}/package.json` dependencies에  
   `@devian/module-common` 존재

**Unity UPM:**

6. Protocol용 `.asmdef` 파일의 `references`에 `Devian + .Module.Common` 존재

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
