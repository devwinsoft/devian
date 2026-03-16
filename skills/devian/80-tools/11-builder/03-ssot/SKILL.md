# 03-ssot — Builder

Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian/10-module/03-ssot/SKILL.md

---

## Scope

이 문서는 **DATA 도메인(테이블, Contract, 스토리지)** 및 **PROTOCOL 도메인(코드젠, Opcode, Registry)** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../../10-module/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

---

## Build Target

빌드는 `-[target]` 옵션으로 실행 범위를 지정한다.

```bash
bash input/build.sh -[target] <buildInputJson>
node framework-ts/tools/builder/build.js -[target] <buildInputJson>
```

| Target | 설명 |
|--------|------|
| `-all` | 전체 빌드 (기본값). domain-code + protocol + domain-data 모두 실행 |
| `-domain-code` | Domain codegen (C#/TS). contracts/tables → C#/TS 코드 생성, UPM domain 패키지, validate, sync |
| `-protocol` | Protocol codegen (C#/TS). protocol JSON → C#/TS 코드 생성, UPM protocol 패키지, validate, sync |
| `-domain-data` | Domain 데이터 파일 출력. tables → NDJSON/pb64 파일 (tableConfig dirs로 출력) |

### Target별 Phase 실행 범위

| Phase | -all | -domain-code | -protocol | -domain-data |
|-------|------|-------------|-----------|--------------|
| Phase 0: Config 로딩 + Guard | ✅ | ✅ | ✅ | ✅ |
| Phase 1: Generate (domain codegen) | ✅ | ✅ | — | — |
| Phase 1: Generate (protocol codegen) | ✅ | — | ✅ | — |
| Phase 1: Generate (data files) | ✅ | — | — | ✅ |
| Phase 2: Materialize (code → module/upm) | ✅ | ✅ | ✅ | — |
| Phase 2: Materialize (data → tableConfig dirs) | ✅ | — | — | ✅ |
| Phase 3: Validate | ✅ | ✅ | ✅ | — |
| Phase 4: Sync (upm → packageDir) | ✅ | ✅ | ✅ | — |
| Phase 5: Sample metadata sync | ✅ | ✅ | ✅ | — |

### Target ↔ 스킬 문서 매핑

| Target | 관련 스킬 문서 |
|--------|---------------|
| `-domain-code` | Input: 30-table-cell-format, 31-table-row-format, 32-table-authoring / Target: 50-contract-codegen, 51-table-codegen, 52-table-enumgen |
| `-domain-data` | Target: 53-data-ndjson, 54-data-pb64 |
| `-protocol` | Input: 33-protocol-spec, 34-protocol-gen-policy / Target: 55-protocol-codegen |

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

상세 규칙: [skills/devian/80-tools/11-builder/33-protocol-spec](../33-protocol-spec/SKILL.md)

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
- staging: `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{Protocol}SessionHost.g.cs`
- staging: `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{Protocol}Networker.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Generated/{ProtocolName}.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Generated/{Protocol}SessionHost.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolGroup}/Generated/{Protocol}Networker.g.cs`
- ~~`ClientSessionHost.g.cs`~~ — 삭제 (프로토콜별 `{Protocol}SessionHost`로 대체)
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

**Protocol은 `com.devian.samples/Samples~/{ProtocolGroup}Protocol/`에 샘플로 생성된다.**

> 상세 규칙: § Sample-target Protocol 참조.

**Protocol UPM은 Runtime-only이며, 빌더가 touch 가능한 범위는 `Runtime/Generated/`뿐이다.**

**final 반영 대상:**
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{ProtocolGroup}Protocol/Runtime/Generated/**`
- 포함 파일: `{Protocol}.g.cs`, `{Protocol}SessionHost.g.cs`, `{Protocol}Networker.g.cs`

**수기/고정 파일 (빌더 생성/수정 금지):**
- `Runtime/Devian.Samples.{ProtocolGroup}Protocol.asmdef`
- `README.md`
- `*.meta`

**레거시 청소:**
- 기존 독립 UPM `com.devian.protocol.{suffix}/` 삭제
- `Editor/` 폴더 존재 시 삭제 (Runtime-only 정책)

### Protocol 충돌 정책 (Hard Fail)

- `protocols` 배열 내에서 동일한 SampleName (`{ProtocolGroup}Protocol`)이 계산되면 빌드 **FAIL**.
- Domain SampleName과의 충돌도 **FAIL** (§ Sample-target 충돌 정책 참조).
- 덮어쓰기/우선순위 없음. 모든 충돌은 명시적 오류.

### Protocol 모듈 의존성 (Hard Rule)

**C# PROTOCOL 모듈 의존성:**
- `Devian.Protocol.{ProtocolGroup}.csproj`는 다음을 ProjectReference 한다:
  - `..\Devian\Devian.csproj`
  - `..\Devian.Domain.Common\Devian.Domain.Common.csproj`

**Unity asmdef 의존성 (Sample):**
- `Devian.Samples.{ProtocolGroup}Protocol` asmdef는 다음을 references 한다:
  - `Devian.Core` (com.devian.foundation)
  - `Devian.Samples.{CommonDomainKey}Package` (com.devian.samples — `getSampleName('Common', 'Package')`)

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

> Common 모듈의 상세 정책은 [skills/devian/20-domain-common/02-module-policy](../../../../devian/20-domain-common/02-module-policy/SKILL.md)를 참조한다.

### 필수 개념

- **Contracts**: JSON 기반 타입/enum 정의
- **Tables**: XLSX 기반 테이블 정의 + 데이터

입력 경로는 `{buildInputJson}`이 정본이다:
- `domains[Common].contractDir = Domains/Common`
- `domains[Common].tableDir = Domains/Common`

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

상세 규칙: [skills/devian/80-tools/11-builder/32-table-authoring](../32-table-authoring/SKILL.md)

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

## Sample 폴더 네이밍 컨벤션 (Hard Rule)

Domain과 Protocol은 독립 UPM 패키지가 아니라, **모두** `com.devian.samples/Samples~/`에 샘플로 생성된다.

### 네이밍 규칙 (동적 파생, 하드코딩 금지)

| 카테고리 | 입력 키 | Suffix | Sample 이름 | Assembly 이름 |
|----------|---------|--------|-------------|---------------|
| Domain | `{DomainKey}` | `Package` | `{DomainKey}Package` | `Devian.Samples.{DomainKey}Package` |
| Protocol | `{ProtocolGroup}` | `Protocol` | `{ProtocolGroup}Protocol` | `Devian.Samples.{ProtocolGroup}Protocol` |
| Manual | 패키지 고유 키 | `Package` | `{Key}Package` | `Devian.Samples.{Key}Package` |

- **빌더 구현:** `DevianToolBuilder.getSampleName(key, suffix)` → `${key}${suffix}`
- **위치:** `com.devian.samples/Samples~/{Key}{Suffix}/`
- **rootNamespace 보존 (Hard Rule):**
  - Domain: `Devian.Domain.{DomainKey}` (원래 도메인 namespace 유지)
  - Protocol: `Devian.Protocol.{ProtocolGroup}` (원래 프로토콜 namespace 유지)

> 새로운 카테고리 추가 시 Suffix를 SSOT에 먼저 정의하고, 빌더에 반영한다 (§5 준수).

**Manual 카테고리:**
- 빌더가 Generated 코드를 주입하지 않는 완전 수동 패키지.
- 현재 대상: `UI` → `UIPackage` (기존 독립 UPM `com.devian.ui` → Sample 이관).
- 레거시 asmdef 참조(`Devian.UI`, `Devian.UI.Editor`)는 `asmdefRefToUpmPackage()`에서 `com.devian.samples`로 매핑.

### 공통 규칙

- 각 Sample의 `asmdef`, `README.md`는 수동 관리 (빌더가 생성/수정 금지).
- `*.meta` 파일은 수동 관리 (Unity GUID 보존).
- 빌더가 touch 가능한 범위는 `Runtime/Generated/`와 `Editor/Generated/`뿐이다.
- Hybrid 감지: target에 Runtime asmdef (`Devian.Samples.{SampleName}.asmdef`)가 존재하면 hybrid mode → `Generated/` 디렉토리만 clean+copy, 수동 addon 보존.
- Hybrid가 아니면 (새 도메인/프로토콜 첫 빌드 등): `copyUpmToTarget()`으로 전체 staging 복사.

### Sample-target 충돌 정책 (Hard Fail)

- `Samples~/` 내에서 동일한 SampleName이 계산되면 빌드 **FAIL**.
- Domain과 Protocol 간에도 SampleName 충돌 시 **FAIL** (예: DomainKey `GameProtocol` + ProtocolGroup `Game` → 둘 다 `GameProtocol` 충돌).
- 덮어쓰기/우선순위 없음. 모든 충돌은 명시적 오류.

---

## Sample-target Domain (Hard Rule)

**모든** 도메인은 `com.devian.samples/Samples~/{DomainKey}Package/`에 샘플로 생성된다.

### 생성 규칙

- Builder는 Domain의 Generated 코드를 `{upmConfig.sourceDir}/com.devian.samples/Samples~/{SampleName}/Runtime/Generated/`와 `Editor/Generated/`에 출력한다.
- `com.devian.domain.{key}` 독립 UPM 패키지는 생성하지 않는다.
- C# staging 경로는 기존과 동일: `{tempDir}/{DomainKey}/cs/Generated/`
- Materialize(Phase 2)에서 staging → target 복사 경로만 변경된다.

### 산출물 경로 (Domain Sample-target)

**staging (변경 없음):**
- `{tempDir}/{DomainKey}/cs/Generated/{DomainKey}.g.cs`
- `{tempDir}/{DomainKey}/upm/` (staging UPM scaffold)

**final:**
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{DomainKey}Package/Runtime/Generated/{DomainKey}.g.cs`
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{DomainKey}Package/Editor/Generated/*.Editor.cs`

---

## Sample-target Protocol (Hard Rule)

**모든** 프로토콜은 `com.devian.samples/Samples~/{ProtocolGroup}Protocol/`에 샘플로 생성된다. 독립 UPM 패키지(`com.devian.protocol.{suffix}`)는 생성하지 않는다.

### Protocol Sample 특성

- **Runtime-only**: Editor 폴더/asmdef 없음.
- **Generated-only**: 빌더가 touch하는 범위는 `Runtime/Generated/`뿐.
- **noEngineReferences: true** — Protocol은 순수 C# 코드이며 UnityEngine 의존 없음.

### 수기/고정 파일 (빌더 생성/수정 금지)

- `Runtime/Devian.Samples.{ProtocolGroup}Protocol.asmdef`
- `README.md`
- `*.meta`

### asmdef 구성

```json
{
    "name": "Devian.Samples.{ProtocolGroup}Protocol",
    "rootNamespace": "Devian.Protocol.{ProtocolGroup}",
    "references": [
        "Devian.Core",
        "Devian.Samples.{CommonDomainKey}Package"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

> `{CommonDomainKey}Package`는 Sample 네이밍 컨벤션에 따라 `getSampleName('Common', 'Package')`로 파생.

### 산출물 경로 (Protocol Sample-target)

**staging (변경 없음):**
- `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{ProtocolName}.g.cs`
- `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{Protocol}SessionHost.g.cs`
- `{tempDir}/Devian.Protocol.{ProtocolGroup}/cs/Generated/{Protocol}Networker.g.cs`

**final (변경됨):**
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{ProtocolGroup}Protocol/Runtime/Generated/{ProtocolName}.g.cs`
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{ProtocolGroup}Protocol/Runtime/Generated/{Protocol}SessionHost.g.cs`
- `{upmConfig.sourceDir}/com.devian.samples/Samples~/{ProtocolGroup}Protocol/Runtime/Generated/{Protocol}Networker.g.cs`

> C# staging(`…/cs/Generated`)을 그대로 Sample `Runtime/Generated/`로 copy한다. 별도 `-upm` staging 없음.

### 레거시 청소

- 기존 독립 UPM 패키지 `{upmConfig.sourceDir}/com.devian.protocol.{suffix}/`는 삭제한다.
- `Packages/com.devian.protocol.{suffix}/`도 삭제한다.
- `manifest.json`에서 `com.devian.protocol.*` 참조를 제거한다.

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

상세 규칙: [skills/devian/80-tools/11-builder/53-data-ndjson](../53-data-ndjson/SKILL.md)

---

## pb64 export 규약 (Hard Rule)

**pk 옵션이 있는 테이블만 Unity TextAsset `.asset` 파일로 export한다.**

- 파일명: `{TableName}.asset` (테이블 단위 1개 파일)
- 저장 형식: Unity TextAsset YAML
- pk 옵션이 없는 테이블은 export 안함

상세 규칙: [skills/devian/80-tools/11-builder/54-data-pb64](../54-data-pb64/SKILL.md)

---

## DATA export PK 규칙 (Hard Rule)

**DATA export는 PK 유효 row만 포함하며, 유효 row가 없으면 산출물을 생성하지 않는다.**

- `primaryKey`(pk 옵션)가 정의되지 않은 테이블은 ndjson/pb64 파일을 생성하지 않는다.
- `primaryKey` 값이 비어있는 row는 export 대상에서 제외된다.
- 결과적으로 유효 row가 0개인 경우 파일을 생성하지 않고 `[Skip]` 로그를 남긴다.

---

## See Also

- [Root SSOT](../../../10-module/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Builder Policy](../01-policy/SKILL.md)
- [Table Authoring Rules](../32-table-authoring/SKILL.md)
- [NDJSON Storage](../53-data-ndjson/SKILL.md)
- [PB64 Storage](../54-data-pb64/SKILL.md)
- [Codegen Protocol](../33-protocol-spec/SKILL.md)
- [Codegen Protocol C#/TS](../34-protocol-gen-policy/SKILL.md)
- [Package Policy](../../../../devian-unity/04-package-policy/SKILL.md) — UPM package.json/Samples~ 정책
