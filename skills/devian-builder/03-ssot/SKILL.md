# 03-ssot — Builder

Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian-core/03-ssot/SKILL.md

---

## Scope

이 문서는 **DATA 도메인(테이블, Contract, 스토리지)** 및 **PROTOCOL 도메인(코드젠, Opcode, Registry)** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../devian-core/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

---

## PROTOCOL SSOT

### DomainType = PROTOCOL

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

### Protocol Spec 포맷

- 입력 파일은 **JSON**이며 `protocolDir` 아래 `protocolFiles`에 명시된 파일을 처리한다.
- 파일명 base를 **ProtocolName**으로 간주한다. (예: `C2Game.json` → `C2Game`)

상세 규칙: [skills/devian-builder/40-codegen-protocol](../40-codegen-protocol/SKILL.md)

### Opcode/Tag 레지스트리 (결정성)

- `{ProtocolName}.opcodes.json`, `{ProtocolName}.tags.json`은 **프로토콜 호환성을 위한 Registry**다.
- Registry 파일은 `protocolDir/Generated/`에 위치하며, 빌드 시 갱신된다.
- Registry는 "생성된 입력" 파일로, 기계가 생성하지만 입력 폴더에 보존된다.

**정책 목표:**
- **결정적(deterministic)** 이여야 한다.
- 명시된 값이 있으면 **명시 값 우선**
- 미지정 값은 **결정적 규칙으로 자동 할당**
- Tag는 Protobuf 호환 범위를 따르며 **reserved range(19000~19999)**는 금지

> "자동 할당의 정확한 규칙(최소값/정렬/증가 방식)"은 코드를 정답으로 본다.

### PROTOCOL 산출물 경로 (정책)

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

### Protocol UPM 산출물 정책 (Hard Rule)

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

### Protocol 충돌 정책 (Hard Fail)

1. `upm`에 동일한 `computedUpmName`이 존재하면 빌드 **FAIL**
2. `upm`에 동일한 `computedUpmName`이 이미 존재하면 빌드 **FAIL** (중복 생성)
3. `protocols` 배열 내에서 동일한 `computedUpmName`이 계산되면 빌드 **FAIL**

> 덮어쓰기/우선순위 없음. 모든 충돌은 명시적 오류.

### Protocol 모듈 의존성 (Hard Rule)

**C# PROTOCOL 모듈 의존성:**
- `Devian.Protocol.{ProtocolGroup}.csproj`는 다음을 ProjectReference 한다:
  - `..\Devian\Devian.csproj`
  - `..\Devian.Domain.Common\Devian.Domain.Common.csproj`

**TS PROTOCOL 패키지 의존성:**
- `@devian/protocol-{protocolgroup}`는 `@devian/core` + `@devian/module-common`을 의존한다.

### TS Runtime Import 경로 규칙 (Hard Fail)

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

## tableConfig 설정

DATA 도메인의 데이터 출력 타겟은 전역 `tableConfig`로 설정한다.

```json
"tableConfig": {
  "tableDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Tables"],
  "stringDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Strings"],
  "soundDirs": ["../framework-cs/apps/UnityExample/Assets/Bundles/Sounds"]
}
```

| 필드 | 역할 | 예시 |
|------|------|------|
| `tableDirs` | 테이블 출력 디렉토리 목록 | `["...Assets/Bundles/Tables"]` |
| `stringDirs` | String 테이블 출력 디렉토리 목록 | `["...Assets/Bundles/Strings"]` |
| `soundDirs` | Sound 데이터 출력 디렉토리 목록 | `["...Assets/Bundles/Sounds"]` |

**필수 규칙:**
- `tableConfig`의 각 Dir 배열은 필수 (빈 배열 허용)
- 빌더가 각 Dir에 대해 `ndjson/` 및 `pb64/` 하위 디렉토리를 생성
- `dataConfig`는 금지 (deprecated, 존재 시 빌드 FAIL)
- `domains[*].dataTargetDirs`는 금지 (존재 시 빌드 실패)

---

## DomainType = DATA

DATA 입력은 `{buildInputJson}`의 `domains` 섹션이 정의한다.

### Common 필수 (Hard Rule)

**Devian v10 프로젝트는 DATA DomainKey로 `Common`을 반드시 포함한다.**

- `{buildInputJson}`에서 `domains.Common`은 필수 항목이다.
- 결과로 Common 모듈(C#/TS)은 항상 생성/유지된다:
  - C#: `Devian.Domain.Common` (프로젝트명)
  - TS: `@devian/module-common` (폴더명: `devian-domain-common`)

> Common 모듈의 상세 정책은 [skills/devian-common/02-module-policy](../../devian-common/02-module-policy/SKILL.md)를 참조한다.

### 필수 개념

- **Contracts**: JSON 기반 타입/enum 정의
- **Tables**: XLSX 기반 테이블 정의 + 데이터

입력 경로는 `{buildInputJson}`이 정본이다:
- `domains[Common].contractDir = Domains/Common/contracts`
- `domains[Common].tableDir = Domains/Common/tables`

**키 변경 (레거시 호환):**
- `contractDir` (새 키), `contractsDir` (레거시/금지)
- `tableDir` (새 키), `tablesDir` (레거시/금지)

---

## Tables (XLSX) 헤더/데이터 규약

- 최소 **4행 헤더**를 가진다.
  - Row 1: 컬럼명
  - Row 2: 타입
  - Row 3: 옵션
  - Row 4: 코멘트(해석하지 않음)
- Row 5부터 데이터
- **Header Stop Rule**: Row1에서 빈 셀을 만나면 그 뒤 컬럼은 무시
- **Data Stop Rule**: PrimaryKey 컬럼이 비면 즉시 중단

### 옵션 해석 정책

- **PrimaryKey:** `pk` 옵션만 PrimaryKey로 해석한다.
- **gen:\<EnumName\>:** `gen:` 옵션이 선언된 컬럼은 **반드시 `pk`여야 한다**.
- **group:true (Hard):** 테이블당 최대 1개 컬럼만 허용.
- `optional:true`는 "nullable/optional column" 힌트로만 사용
- 그 외 `parser:*` 등은 **Reserved** (있어도 무시 / 의미 부여 금지)

상세 규칙: [skills/devian-builder/30-table-authoring-rules](../30-table-authoring-rules/SKILL.md)

---

## DATA 산출물 경로 (정책)

**staging:**
- `{tempDir}/{DomainKey}/cs/Generated/{DomainKey}.g.cs`
- `{tempDir}/{DomainKey}/ts/Generated/{DomainKey}.g.ts`, `index.ts`
- `{tempDir}/{DomainKey}/data/ndjson/{TableName}.json` (내용은 NDJSON)
- `{tempDir}/{DomainKey}/data/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)

**final (csConfig/tsConfig/tableConfig 기반):**
- `{csConfig.generateDir}/Devian.Domain.{DomainKey}/Generated/{DomainKey}.g.cs`
- `{tsConfig.generateDir}/devian-domain-{domainkey}/Generated/{DomainKey}.g.ts`, `index.ts`
- `{tableDir}/ndjson/{TableName}.json` (내용은 NDJSON)
- `{tableDir}/pb64/{TableName}.asset` (pk 옵션 있는 테이블만)

**도메인 폴더 미사용 (Hard Rule):**
- 최종 경로에 `{DomainKey}` 폴더를 생성하지 않는다.
- 모든 도메인의 테이블 파일이 동일 디렉토리에 병합된다.
- **동일 파일명 충돌 시 빌드 FAIL** (조용한 덮어쓰기 금지).

**금지 필드 (Hard Fail):**
- `domains[*].csTargetDir` — 금지, `csConfig.generateDir` 사용
- `domains[*].tsTargetDir` — 금지, `tsConfig.generateDir` 사용
- `domains[*].dataTargetDirs` — 금지, `tableConfig.*Dirs` 사용

---

## C# Namespace (Hard Rule)

DATA Domain 생성물의 C# 네임스페이스:

- `namespace Devian.Domain.{DomainKey}`

예: DomainKey `Common` → `namespace Devian.Domain.Common`

---

## TS index.ts Marker 관리 (Hard Rule)

**TS `devian-domain-*/index.ts`는 빌더가 관리하되, 통째 덮어쓰기를 금지한다.**

- marker 구간:
  - `// <devian:domain-exports>` ~ `// </devian:domain-exports>` — Domain 생성물 export
  - `// <devian:feature-exports>` ~ `// </devian:feature-exports>` — features 폴더 export

---

## NDJSON 스토리지 규약

**파일 확장자는 `.json`이지만, `ndjson/` 폴더의 파일 내용은 NDJSON(라인 단위 JSON)이다.**

상세 규칙: [skills/devian-builder/34-ndjson-storage](../34-ndjson-storage/SKILL.md)

---

## pb64 export 규약 (Hard Rule)

**pk 옵션이 있는 테이블만 Unity TextAsset `.asset` 파일로 export한다.**

- 파일명: `{TableName}.asset` (테이블 단위 1개 파일)
- 저장 형식: Unity TextAsset YAML
- pk 옵션이 없는 테이블은 export 안함

상세 규칙: [skills/devian-builder/35-pb64-storage](../35-pb64-storage/SKILL.md)

---

## DATA export PK 규칙 (Hard Rule)

**DATA export는 PK 유효 row만 포함하며, 유효 row가 없으면 산출물을 생성하지 않는다.**

- `primaryKey`(pk 옵션)가 정의되지 않은 테이블은 ndjson/pb64 파일을 생성하지 않는다.
- `primaryKey` 값이 비어있는 row는 export 대상에서 제외된다.
- 결과적으로 유효 row가 0개인 경우 파일을 생성하지 않고 `[Skip]` 로그를 남긴다.

---

## See Also

- [Root SSOT](../../devian-core/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Builder Policy](../01-policy/SKILL.md)
- [Table Authoring Rules](../30-table-authoring-rules/SKILL.md)
- [NDJSON Storage](../34-ndjson-storage/SKILL.md)
- [PB64 Storage](../35-pb64-storage/SKILL.md)
- [Codegen Protocol](../40-codegen-protocol/SKILL.md)
- [Codegen Protocol C#/TS](../41-codegen-protocol-csharp-ts/SKILL.md)
- [Package Metadata](../../devian-unity/04-package-metadata/SKILL.md) — UPM package.json 정책
