# 03-ssot — Protocol

Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian-core/03-ssot/SKILL.md

---

## Scope

이 문서는 **PROTOCOL 도메인(코드젠, Opcode, Registry)** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../devian-core/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

---

## DomainType = PROTOCOL

PROTOCOL 입력은 `{buildInputJson}`의 `protocols` 섹션(배열)이 정의한다.

```json
"protocols": [
  {
    "group": "Game",
    "protocolDir": "./Protocols/Game",
    "protocolFiles": ["C2Game.json", "Game2C.json"]
  }
]
```

**필드 정의:**

| 필드 | 의미 | 필수 |
|------|------|------|
| `group` | ProtocolGroup 이름 | ✅ |
| `protocolDir` | 프로토콜 JSON 파일 디렉토리 | ✅ |
| `protocolFiles` | 처리할 프로토콜 파일 목록 | ✅ |

**금지 필드 (Hard Fail):**
- `csTargetDir` — 금지, `csConfig.generateDir` 사용
- `tsTargetDir` — 금지, `tsConfig.generateDir` 사용
- `upmTargetDir` — 금지, 사용 시 빌드 실패
- `upmName` — 금지, 자동 계산됨

---

## Protocol Spec 포맷

- 입력 파일은 **JSON**이며 `protocolDir` 아래 `protocolFiles`에 명시된 파일을 처리한다.
- 파일명 base를 **ProtocolName**으로 간주한다. (예: `C2Game.json` → `C2Game`)

상세 규칙: [skills/devian-protocol/40-codegen-protocol](../40-codegen-protocol/SKILL.md)

---

## Opcode/Tag 레지스트리 (결정성)

- `{ProtocolName}.opcodes.json`, `{ProtocolName}.tags.json`은 **프로토콜 호환성을 위한 Registry**다.
- Registry 파일은 `protocolDir/Generated/`에 위치하며, 빌드 시 갱신된다.
- Registry는 "생성된 입력" 파일로, 기계가 생성하지만 입력 폴더에 보존된다.

**정책 목표:**
- **결정적(deterministic)** 이여야 한다.
- 명시된 값이 있으면 **명시 값 우선**
- 미지정 값은 **결정적 규칙으로 자동 할당**
- Tag는 Protobuf 호환 범위를 따르며 **reserved range(19000~19999)**는 금지

> "자동 할당의 정확한 규칙(최소값/정렬/증가 방식)"은 코드를 정답으로 본다.

---

## PROTOCOL 산출물 경로 (정책)

**C# (ProtocolGroup = {ProtocolGroup}):**
- staging: `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{ProtocolName}.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Generated/{ProtocolName}.g.cs`
- 프로젝트 파일: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Devian.Protocol.{ProtocolGroup}.csproj` (수기/고정, 빌더가 생성/수정 금지)
- namespace: `Devian.Protocol.{ProtocolGroup}` (변경 금지)

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/ts/Generated/{ProtocolName}.g.ts`
- final: `{tsConfig.generateDir}/devian-protocol-{protocolgroup}/Generated/{ProtocolName}.g.ts`
- `index.ts`는 모듈 루트에 존재하되 수기/고정, 빌더가 생성/수정 금지
- 패키지명: `@devian/protocol-{protocolgroup}` (기존 유지)

> **생성물 namespace 고정 (Hard Rule):**
> C# 생성물 namespace는 `Devian.Protocol.{ProtocolGroup}`으로 고정이며, 런타임 모듈 단일화와 무관하게 변경하지 않는다.

---

## Protocol UPM 산출물 정책 (Hard Rule)

**Protocol UPM은 Runtime-only이며, 빌더가 touch 가능한 범위는 Generated/** 뿐이다.**

**UPM 산출물 경로:**
- staging 생성/수정 허용 대상: `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/**`
- final 반영 대상 (빌더가 touch 가능한 범위): `{upmConfig.sourceDir}/com.devian.protocol.{suffix}/Runtime/Generated/**`

> UPM은 별도 `-upm` staging을 만들지 않으며, C# staging(`…/cs/Generated`)을 그대로 UPM `Runtime/Generated`로 copy한다.

**수기/고정 파일 (빌더 생성/수정 금지):**
- `package.json`
- `Runtime/*.asmdef`
- `*.meta`

**레거시 청소:**
- `Editor/` 폴더 존재 시 삭제 (Runtime-only 정책)

**UPM 패키지명 자동 계산:**
```
computedUpmName = "com.devian.protocol." + normalize(group)
```

**normalize(group) 규칙:**
1. 소문자화
2. 공백은 `_`(underscore)로 치환
3. `_`(underscore)는 유지
4. 허용 문자: `[a-z0-9._-]`만 유지, 그 외 제거
5. 앞/뒤의 `.`, `-`, `_` 정리

**예시:**
| group | computedUpmName |
|-------|-----------------|
| `Game` | `com.devian.protocol.game` |
| `Game_Server` | `com.devian.protocol.game_server` |
| `Auth Service` | `com.devian.protocol.auth_service` |

---

## 충돌 정책 (Hard Fail)

1. `upm`에 동일한 `computedUpmName`이 존재하면 빌드 **FAIL**
2. `upm`에 동일한 `computedUpmName`이 이미 존재하면 빌드 **FAIL** (중복 생성)
3. `protocols` 배열 내에서 동일한 `computedUpmName`이 계산되면 빌드 **FAIL**

> 덮어쓰기/우선순위 없음. 모든 충돌은 명시적 오류.

---

## 모듈 의존성 (Hard Rule)

**C# PROTOCOL 모듈 의존성:**
- `Devian.Protocol.{ProtocolGroup}.csproj`는 다음을 ProjectReference 한다:
  - `..\Devian\Devian.csproj`
  - `..\Devian.Domain.Common\Devian.Domain.Common.csproj`

**TS PROTOCOL 패키지 의존성:**
- `@devian/protocol-{protocolgroup}`는 `@devian/core` + `@devian/module-common`을 의존한다.

---

## TS Runtime Import 경로 규칙 (Hard Fail)

**`Generated/*Runtime.g.ts`가 같은 `Generated/` 폴더의 `*.g.ts`를 import할 때 상대경로는 반드시 `./`를 사용한다.**

```typescript
// ✅ CORRECT
import { C2Game } from './C2Game.g';
import { Game2C } from './Game2C.g';

// ❌ WRONG - 즉시 FAIL
import { C2Game } from '../C2Game.g';  // <- ../는 금지
```

**검증 DoD:**
- `npm -w game-server run start` 실행 시 `ERR_MODULE_NOT_FOUND` 없음
- `npm -w game-client run dev` 실행 시 `ERR_MODULE_NOT_FOUND` 없음

> Generator 수정 시 이 규칙을 반드시 유지해야 한다. (위치: `protocol-ts.js`의 `generateServerRuntime()`, `generateClientRuntime()`)

---

## See Also

- [Root SSOT](../../devian-core/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Package Metadata](../../devian-unity/04-package-metadata/SKILL.md) — UPM package.json 정책 (author.name, displayName 등)
- [Protocol Policy](../01-policy/SKILL.md)
- [Codegen Protocol](../40-codegen-protocol/SKILL.md)
