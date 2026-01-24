# Devian v10 — SSOT (Policy Only)

Status: ACTIVE  
AppliesTo: v10  
SSOT: this file

## Purpose

이 문서는 **Devian v10의 정책(Policy)만**을 정의한다.

- **입력 규약 / 경로 / 검증 규칙 / 용어**만 포함한다.
- **코드에 종속된 내용(생성 클래스 목록, 인터페이스 시그니처, 프레임 바이너리 레이아웃, 런타임 API 등)**은 여기서 다루지 않는다.
- 코드/생성물/시그니처/프레임 포맷의 정답은 **런타임/제너레이터 코드**이다.

---

## SSOT 우선순위

1. **`skills/devian/03-ssot/SKILL.md`** — 정책(Policy) 정본
2. **`input_common.json`** — 실제 빌드 스키마/경로 정본
3. **런타임/제너레이터 코드** — 실제 동작 정본

SSOT 간 충돌이 발생하면:

- **정책(이 문서) ↔ input_common.json** 충돌은 **input_common.json이 우선**
- **정책 ↔ 코드** 충돌은 **코드가 우선**
- 정책을 유지하고 싶다면(코드가 아니라 정책이 정답이어야 한다면) **코드를 바꾸는 결정**이 필요하다.

---

## "충돌"의 의미 (필수)

Devian 문서/대화에서 말하는 "충돌"은 기능 자체의 찬반/의견 충돌이 아니다.

- **SSOT 불일치(Hard)**: 문서(SKILL/input_common.json)에 적힌 규약/경로/정책이 실제 코드/설정/산출물과 **다른 상태**
  - 예: 문서에는 `excludePlatforms: ["WebGL"]`인데 실제 asmdef는 `excludePlatforms: []`
  - 예: 문서에는 `WsNetClient`인데 실제 코드는 `WebSocketClient`
- 결론: "WebGL 지원" 같은 기능은 가능/불가능이 아니라, **문서와 구현의 일치 여부**만 문제다.

---

## 용어 (필수)

문서/대화에서 아래 용어를 강제한다. **"domain" 단독 사용 금지**.

| 용어 | 의미 | 예시 |
|---|---|---|
| **DomainType** | 종류 | `DATA`, `PROTOCOL` |
| **DomainKey** | DATA 도메인의 input_common.json 키 | `Common` |
| **ProtocolGroup** | PROTOCOL 그룹명 (input_common.json `group` 필드) | `Client` |
| **ProtocolName** | PROTOCOL 파일명 base | `C2Game`, `Game2C` |

---

## 플레이스홀더 표준 (필수)

문서/대화에서 `{domain}`, `{name}` 같은 범용 플레이스홀더를 금지한다.

허용 플레이스홀더:

- `{tempDir}` — input_common.json의 `tempDir` 값
- `{DomainKey}`
- `{ProtocolGroup}`
- `{ProtocolName}`
- `{csConfig.generateDir}`, `{tsConfig.generateDir}` — 전역 C#/TS 반영 루트
- `{dataConfig.tableDirs}` — 전역 데이터 반영 타겟 (배열)
- `{upmTargetDir}` — deprecated, upmConfig로 계산

> `{dataConfig.tableDirs}`는 배열이다. 문서에서 배열 내 개별 요소를 지칭할 때 `{dataTargetDir}`로 표기할 수 있다.

> `{tempDir}`는 절대 경로가 아닌 경우 **input_common.json이 있는 디렉토리** 기준으로 해석한다.

---

## 빌드 파이프라인 정책

### Input 포맷 분리 (Hard Rule)

**빌드 설정은 config.json과 input.json으로 분리한다.**

| 파일 | 역할 | 허용 키 |
|------|------|---------|
| `input/config.json` | 공통 설정 (경로/타겟) | csConfig, tsConfig, dataConfig, upmConfig, staticUpmPackages |
| `input/input_*.json` | 빌드 스펙 (도메인/프로토콜) | version, configPath, tempDir, domains, protocols |

**금지 키 (Hard FAIL):**
- config.json에 `tempDir`, `domains`, `protocols` 존재 → FAIL
- input.json에 `csConfig`, `tsConfig`, `dataConfig`, `upmConfig`, `staticUpmPackages` 존재 → FAIL

**상대경로 기준 (중요):**
- 모든 상대경로 해석 기준은 **input json 파일이 있는 폴더 (buildJsonDir, 보통 `input/`)**
- config.json 자신의 디렉토리를 기준으로 해석하면 **FAIL**

**머지 규칙:**
```
finalConfig = deepMerge(config.json, input.json)
```
- tempDir은 input.json 값이 최종값 (input 우선)
- 그 외 키가 양쪽에 있으면 **FAIL**

**예시 (input/input_common.json):**
```json
{
  "version": "10",
  "configPath": "./config.json",
  "tempDir": "temp",
  "domains": { "Common": { ... } },
  "protocols": [ ... ]
}
```

**예시 (input/config.json):**
```json
{
  "configVersion": 1,
  "csConfig": { "moduleDir": "../framework-cs/module", "moduleDir" (unified): "../framework-cs/module" },
  "tsConfig": { "moduleDir": "../framework-ts/module", "moduleDir" (unified): "../framework-ts/module" },
  "upmConfig": { "sourceDir": "../framework-cs/upm", "packageDir": "..." },
  "dataConfig": { "tableDirs": [...] },
  "staticUpmPackages": [...]
}
```

### Phase 모델

빌드는 **4단계**로 해석한다.

1. **Generate**: 모든 산출물은 **staging({tempDir})에만 생성**
2. **Materialize**: staging → module/upm/ts-modules로 **clean + copy** (모듈/패키지 단위)
3. **Validate**: module/module과 upm의 **완결성 검증**
4. **Sync**: upm → packageDir로 **패키지 단위 동기화**

> staging({tempDir}) 외의 위치에 직접 생성하는 동작은 금지한다.
> 
> **Templates 참고:** 샘플/예제 코드는 `framework-cs/upm/com.devian.templates/Samples~/`에서 관리 (UPM Samples~ 사용)
> → `skills/devian-templates/01-templates-policy/SKILL.md`

**반영 위치:**
- C# 생성물: `staging` → `csConfig.generateDir` (framework-cs/module)
- TS 생성물: `staging` → `tsConfig.generateDir` (framework-ts/module)
- UPM 생성물: `staging` → `upmConfig.sourceDir` (framework-cs/upm) - 생성 패키지는 직접 upm에 반영
- 최종 UPM: `upm` → `upmConfig.packageDir`

### UPM Packages Sync 정본 (Hard Rule)

**Packages는 derived output이며 직접 수정 금지.**

| 구분 | 경로 | 역할 |
|------|------|------|
| 정본 (수동) | `framework-cs/upm/{pkg}` | 수동 관리 패키지 원본 |
| 정본 (생성) | `framework-cs/upm/{pkg}` | 빌더가 생성하는 패키지 원본 |
| 복사본 (실행) | `framework-cs/apps/UnityExample/Packages/{pkg}` | Unity 실행용 복사본 |

**소스 우선순위:**
1. `upm/{pkg}` 존재 → upm에서 복사 (hybrid 포함)
2. `upm/{pkg}` 없음 → upm에서 복사

**Hard DoD: Packages 동기화 불일치 FAIL**

sync 후 아래 조건이면 **즉시 FAIL**:
- `Packages/{pkg}`가 선택된 소스(upm 또는 upm)와 내용이 다름
- 정본 소스에 있는데 Packages에 반영되지 않음
- Packages에서 직접 수정한 코드 발견 (다음 sync에서 덮어써짐)

**필수 검증 대상 패키지:**
- `com.devian.core`
- `com.devian.unity.network`
- `com.devian.unity.common`

**수동 패키지 수정 시 필수 절차:**
1. `upm/{pkg}` 또는 `upm/{pkg}`에서 수정
2. 빌더 실행 또는 수동 sync (clean + copy)
3. `Packages/{pkg}` 반영 확인

> **WARNING:** `Packages/` 직접 수정은 정책 위반이며, sync 시 손실된다.

**통합 모드 (HARD RULE):**

`generateDir`가 설정되지 않으면 **통합 모드**로 동작한다:
- `csGenerateDir = csConfig.moduleDir`
- `tsGenerateDir = tsConfig.moduleDir`

통합 모드에서는 `moduleDir`이 생성 출력 루트를 겸한다. 이 경우 "수동 폴더 보호" 규칙은 적용되지 않으며, 빌더가 해당 경로에 생성물을 반영할 수 있다.

**수동 폴더 보호 (분리 모드에서만 적용):**
- `csConfig.moduleDir` (framework-cs/module) — **분리 모드**에서는 빌드가 **생성/삭제/클린/복사 금지**, 검증만 허용
- `tsConfig.moduleDir` (framework-ts/module) — **분리 모드**에서는 빌드가 **생성/삭제/클린/복사 금지**, 검증만 허용
- `upmConfig.sourceDir` (framework-cs/upm) — 빌드가 **생성/삭제/클린/복사 금지**, 검증만 허용

> 분리 모드: `generateDir`가 `moduleDir`와 다른 경로로 명시적으로 설정된 경우

### tsConfig 설정 (v10)

TS 산출물의 반영 위치를 관리하는 설정:

```json
"tsConfig": {
  "moduleDir": "../framework-ts/module"
}
```

| 필드 | 역할 | 필수 | 예시 |
|------|------|------|------|
| `moduleDir` | TS 모듈 루트 | ✅ | `../framework-ts/module` |
| `generateDir` | 생성 TS 모듈 루트 (선택) | ❌ | `../framework-ts/module-gen` |

**통합 모드 (기본):**
- `generateDir`를 생략하면 `moduleDir`을 생성 출력 루트로 사용
- 빌더는 `moduleDir` 하위에 staging 결과를 **clean+copy**

**분리 모드 (선택):**
- `generateDir`를 `moduleDir`와 다른 경로로 명시하면 분리 모드
- 이 경우 `moduleDir`은 검증만, `generateDir`에만 clean+copy

**우선순위 규칙:**
- Domain: `tsConfig.generateDir` (없으면 `tsConfig.moduleDir`)로 반영 (domains[*].tsTargetDir는 deprecated)
- Protocol: `tsConfig.generateDir` (없으면 `tsConfig.moduleDir`)로 반영 (protocols[*].tsTargetDir는 deprecated)

### dataConfig 설정

DATA 도메인의 데이터 출력 타겟은 전역 `dataConfig`로 설정한다.

```json
"dataConfig": {
  "tableDirs": [
    "../output/table",
    "../framework-cs/apps/UnityExample/Assets/Bundles/Tables"
  ]
}
```

| 필드 | 역할 | 예시 |
|------|------|------|
| `tableDirs` | 테이블 데이터 출력 디렉토리 목록 | `["../output/table", "..."]` |

**필수 규칙:**
- `dataConfig.tableDirs`는 필수 (빈 배열 허용)
- 모든 도메인의 데이터 출력이 이 타겟들로 반영됨
- `domains[*].dataTargetDirs`는 deprecated (존재 시 빌드 실패)

### 디렉토리 역할 정의 (SSOT)

| 디렉토리 | 역할 | 빌드 동작 |
|----------|------|-----------|
| `framework-cs/module` | 수동 C# 모듈 (Devian — 단일 통합 모듈) | 검증만, 수정 금지 |
| `framework-cs/module` | 생성 C# 모듈 (Devian.Module.*, Devian.Protocol.*) | staging 결과로 생성/반영 |
| `framework-ts/module` | 수동 TS 모듈 (devian — 단일 통합 모듈) | 검증만, 수정 금지 |
| `framework-ts/module` | 생성 TS 모듈 (devian-module-*, devian-network-*) | staging 결과로 생성/반영 |
| `framework-cs/upm` | 수동 UPM 패키지 (com.devian.core, com.devian.unity.*, com.devian.templates) | 검증만, 수정 금지 |
| `framework-cs/upm` | 생성 UPM 패키지 (com.devian.module.*, com.devian.protocol.*) | staging 결과로 생성/반영 |
| `framework-cs/apps/UnityExample/Packages` | Unity 최종 패키지 | upm + upm → sync |

### C# 런타임 모듈 구조 (Hard Rule)

**Devian C# 런타임은 단일 모듈(단일 csproj)로 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| 단일 모듈 | `framework-cs/module/Devian/Devian.csproj` | Core + Network + Protobuf 통합 |

**런타임 namespace 규약 (Hard Rule):**

| 기능 | namespace |
|------|-----------|
| Core (파서, 엔티티) | `Devian` |
| Network (프레임, 클라이언트) | `Devian` |
| Protobuf (DFF, 변환기) | `Devian` |

> **런타임은 `namespace Devian` 단일을 사용한다.**
> 기능별 하위 네임스페이스(분리된 Net/Proto 네임스페이스 등)는 금지.
> 파일 폴더 구조(Net/, Proto/)는 유지하되 namespace는 통일한다.

**네트워크 API 네이밍 규칙 (Net 접두사):**

런타임 네임스페이스 단일화로 인해, 과거 분리 네임스페이스에 있던 네트워크 계열 public API는 `Net` 접두사로 명확화한다:

| 원래 이름 | 변경 후 | 비고 |
|-----------|---------|------|
| `NetworkClient` | `NetClient` | 중복 방지 축약 |
| `WebSocketClient` | `NetWsClient` | 중복 방지 축약 |
| `HttpRpcClient` | `NetHttpRpcClient` | 접두사 추가 |
| `PacketEnvelope` | `NetPacketEnvelope` | 접두사 추가 |
| `FrameV1` | `NetFrameV1` | 접두사 추가 |
| `IPacketSender` | `INetPacketSender` | 접두사 추가 |
| `INetRuntime` | `INetRuntime` | 이미 Net 포함, 유지 |

> 이미 이름에 `Net`이 포함된 타입은 중복 접두사 금지.
> `Dff*`, `Protobuf*`, `IProto*` 등 의미가 명확한 Proto 계열 타입은 이름 변경 금지.

**생성물 namespace 유지 (Hard Rule):**
- 프로토콜 생성물은 `Devian.Protocol.{ProtocolName}`을 유지한다.
- 모듈 생성물은 `Devian.Module.{DomainKey}`를 유지한다.
- 생성물 namespace는 본 정책에서 변경 금지.

**분리 네임스페이스 금지 게이트 (Hard Fail):**

코드/UPM 패키지/생성물에서 아래 패턴이 1개라도 발견되면 **빌드 FAIL**:

| 금지 패턴 | 설명 |
|-----------|------|
| 분리 Core 네임스페이스 선언/참조 | 런타임에 분리된 Core 네임스페이스 금지 |
| 분리 Net 네임스페이스 선언/참조 | 런타임에 분리된 Net 네임스페이스 금지 |
| 분리 Proto 네임스페이스 선언/참조 | 런타임에 분리된 Proto 네임스페이스 금지 |

검증 대상 경로:
- `framework-cs/module/Devian/`
- `framework-cs/upm/`
- `framework-cs/apps/UnityExample/Packages/`

> 프로토콜 생성물(`Devian.Protocol.*`)은 이 검증에서 제외된다.

### TS 런타임 모듈 구조 (Hard Rule)

**Devian TS 런타임은 단일 패키지(@devian/core)로 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| 단일 패키지 | `framework-ts/module/devian` | Core + Network + Protobuf 통합 |

**패키지 exports:**

| export path | 역할 |
|-------------|------|
| `@devian/core` | 루트 (전체 re-export) |
| `@devian/core/net` | Network 모듈 |
| `@devian/core/proto` | Protobuf 모듈 |
| `@devian/core/transport` | WsTransport |
| `@devian/core/protocol` | NetworkServer |
| `@devian/core/codec` | codec |
| `@devian/core/frame` | frame |

**생성물 패키지명 유지 (Hard Rule):**
- 프로토콜 생성물은 `@devian/network-{protocolgroup}` 이름을 유지한다.
- 모듈 생성물은 `@devian/module-{domainkey}` 이름을 유지한다.

### Unity UPM 패키지 구조 (Hard Rule)

**Devian Unity 런타임은 단일 패키지(com.devian.core)로 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| 단일 패키지 | `framework-cs/upm/com.devian.core` | Core + Network + Protobuf 통합 |

**패키지 내부 asmdef (Hard Rule):**

`com.devian.core`는 **단일 어셈블리**를 제공한다:

| asmdef | namespace | 역할 |
|--------|-----------|------|
| `Devian.Core` | `Devian` | 전체 런타임 기능 (Core + Net + Proto 통합) |

> **asmdef 단일화 정책:**
> - 다른 패키지가 Devian 런타임을 참조할 때는 `"Devian.Core"`만 references에 추가한다.
> - `"Devian.Network"`, `"Devian.Protobuf"` asmdef 참조는 금지.
> - asmdef 이름은 `Devian.Core`이지만, 내부 코드의 namespace는 모두 `Devian`이다.

**생성물 패키지명 유지 (Hard Rule):**
- 프로토콜 생성물은 `com.devian.protocol.{protocolgroup}` 이름을 유지한다.
- 모듈 생성물은 `com.devian.module.{domainkey}` 이름을 유지한다.

**패키지 단일화 (Hard Rule):**
- `com.devian.network`, `com.devian.protobuf`는 더 이상 존재하지 않는다.
- 모든 런타임 기능은 `com.devian.core` 단일 패키지에 포함된다.

### Validate 단계 (Hard Rule)

**C# 모듈 검증:**
- `module` (수동): 각 모듈에 `.csproj` 존재, "완벽한 C# 모듈"
- `module` (생성): 각 모듈에 `.csproj` 존재, "완벽한 C# 모듈"

**UPM 패키지 검증:**
- `upm` (수동): 각 패키지에 `package.json` 존재, 폴더명 == `package.json.name`
- `upm` (생성): 각 패키지에 `package.json` 존재, 폴더명 == `package.json.name`

> "완벽한 C# 모듈" = `.csproj` 존재 + ProjectReference 경로 유효 + dotnet build 가능

### tempDir 경로 해석 및 분리 (Hard Rule)

**`{tempDir}`는 input_common.json(또는 input_sample.json)이 위치한 디렉토리 기준 상대경로로 해석된다.**

빌더는 실행 시 `{tempDir}`를 **clean(rm -rf) 후 재생성**한다. 따라서:

- **동일 repo에서 `input_common.json`과 `input_sample.json`을 번갈아 실행하는 경우, tempDir을 공유하면 서로 staging을 삭제할 수 있으므로 tempDir 분리는 필수다.**

| 설정 파일 | tempDir 권장값 |
|----------|---------------|
| `input_common.json` | `"temp"` |
| `input_sample.json` | `"temp_sample"` |

예시:
- `input_common.json`의 `tempDir: "temp"` → `input/temp/`
- `input_sample.json`의 `tempDir: "temp_sample"` → `input/temp_sample/`

### Clean + Copy 정책

- Copy 단계는 targetDir을 **clean 후 copy**한다.
- **충돌 방지 책임은 input_common.json 설계(타겟 분리)에 있다.**
  - 동일 targetDir에 여러 DomainKey/ProtocolGroup을 매핑하면, clean 단계 때문에 서로 덮어쓰거나 삭제될 수 있다.

---

## 입력 규약

### 경로 해석 규칙

input_common.json 위치는 유동적이다. 현재 프로젝트에서는 `input/input_common.json`에 위치한다.

**input_common.json 내의 모든 상대 경로는 input_common.json이 위치한 디렉토리 기준으로 해석된다.**

예시 (input_common.json이 `input/input_common.json`에 있을 때):
- `contractDir: "Common/contracts"` → `input/Common/contracts`
- `csTargetDir: "../framework/cs"` → `framework/cs`
- `tempDir: "temp"` → `input/temp`

### UPM 전역 설정 (upmConfig) — Hard Rule

**input_common.json은 반드시 `upmConfig` 섹션을 포함해야 한다.**

```json
"upmConfig": {
  "sourceDir": "../framework-cs/upm",
  // generateDir removed (upm single SSOT),
  "packageDir": "../framework-cs/apps/UnityExample/Packages"
}
```

| 필드 | 의미 | 필수 |
|------|------|------|
| `sourceDir` | UPM 소스 루트 — 수동 관리 패키지 (upm) | ✅ |
| `packageDir` | Unity Packages 루트 (UnityExample/Packages) | ✅ |

`upmConfig`가 없거나 필드가 누락되면 빌더는 **하드 실패(throw Error)**한다.

### C# 전역 설정 (csConfig) — 권장

**C# 프로젝트 경로를 전역으로 관리한다.**

```json
"csConfig": {
  "moduleDir": "../framework-cs/module",
  "moduleDir" (unified): "../framework-cs/module"
}
```

| 필드 | 의미 | 필수 |
|------|------|------|
| `moduleDir` | C# 모듈 루트 — 수동 관리 (Devian) | 권장 |
| `generateDir` | C# 생성 루트 — 빌드 생성 (Devian.Module.*, Devian.Protocol.*) | 권장 |

### UPM 동기화 흐름 (Hard Rule)

빌드 최종 단계에서 다음 동기화가 수행된다:

1. **staging(tempDir)** → **upm**: Domain/Protocol UPM 패키지 생성
2. **upm + upm** → **packageDir**: 최종 동기화

**동기화 규칙:**
- 패키지 단위 clean+copy (packageDir 전체 rm -rf 금지)

> **참고:** UPM Samples~는 더 이상 사용하지 않습니다.
> 샘플/예제는 `framework-cs/upm/com.devian.templates/Samples~/`에서 Templates로 관리합니다.

**충돌 정책 (HARD RULE):**
- upm와 upm에 **동일 `package.json.name`이 있으면 무조건 빌드 FAIL**
- 예외 없음 — 둘 다 "완벽한 UPM"이므로 이름이 같으면 정본이 모호함
- 충돌 해결: 패키지 이름 변경 또는 하나 제거

> **왜 충돌 예외를 허용하지 않나?**  
> upm는 수동 관리, upm은 빌드 생성. 둘 다 "완벽한 UPM 패키지"로서 동일한 자격을 가진다.
> 같은 이름의 패키지가 양쪽에 있으면 어느 것이 정본인지 모호해지므로, 빌드 시점에 즉시 FAIL하여 명확한 정리를 강제한다.

### Static UPM Packages

정적 UPM 패키지(수동 관리 + 빌드 시 가공)는 `staticUpmPackages` 배열로 정의한다.
이 패키지들은 **upm에 존재해야 하며**, 빌드 시 staging으로 복사 후 가공되어 upm에 materialize된다.

**`staticUpmPackages`는 string[] 형태로 패키지명만 나열한다:**

```json
"staticUpmPackages": [
  "com.devian.unity.common",
  "com.devian.unity.network"
]
```

**빌드 흐름 (Hard Rule):**

1. **입력 검증**: `upm/{upmName}` 경로에 패키지 존재 여부 검증 (없으면 FAIL)
2. **Staging**: `upm` → `{tempDir}/static-{upmName}` 복사
3. **가공/생성**: staging에서 생성물 생성 (예: `Editor/Generated/*.cs`)
4. **Materialize**: staging → `upm/{upmName}` clean+copy
5. **packageDir Sync**: `upm/{upmName}` → `{packageDir}/{upmName}` (upm이 정본)

**하이브리드 예외 (충돌 정책):**
- staticUpmPackages는 upm와 upm에 **동일 이름**이 공존할 수 있다
- 이 경우 **upm이 정본**이며, upm는 "입력 템플릿"으로만 취급
- packageDir sync에서 upm는 스킵되고 upm에서만 복사됨
- 그 외 패키지는 기존 규칙대로 "동일 이름이면 FAIL" 유지

**GUARD (Hard FAIL):**
- staticUpmPackages 중 upm에 존재하는 패키지는 빌드 후 upm에도 반드시 존재해야 함
- staging 생성이 누락되거나 upm 복사가 실패하면 FAIL
- 이 가드가 "생성물이 버려지는 문제"를 재발 방지함

**Deprecated (Hard Fail):**
- 객체 형태 `{ "upmName": "..." }` 사용 시 빌드 실패 — 반드시 문자열 배열 사용

**경로 계산 규칙 (Hard Rule):**

```
입력(템플릿):    {upmConfig.sourceDir}/{upmName}         (upm)
staging:        {tempDir}/static-{upmName}
materialize:    {upmConfig.sourceDir}/{upmName}       (upm)
최종 패키지:    {upmConfig.packageDir}/{upmName}        (packageDir)
```

예시 (`"com.devian.unity.common"`):
- 입력: `../framework-cs/upm/com.devian.unity.common`
- staging: `{tempDir}/static-com.devian.unity.common`
- materialize: `../framework-cs/upm/com.devian.unity.common`
- 최종: `../framework-cs/apps/UnityExample/Packages/com.devian.unity.common`

### 1) DomainType = DATA

DATA 입력은 input_common.json의 `domains` 섹션이 정의한다.

#### Common 필수 (Hard Rule)

**Devian v10 프로젝트는 DATA DomainKey로 `Common`을 반드시 포함한다.**

- `input/input_common.json`에서 `domains.Common`은 필수 항목이다.
- 결과로 Common 모듈(C#/TS)은 항상 생성/유지된다:
  - C#: `Devian.Module.Common`
  - TS: `@devian/module-common` (폴더명: `devian-module-common`)

> Common 모듈의 상세 정책(생성물/수동 코드 경계, features 구조)은 `skills/devian-common/01-module-policy/SKILL.md`를 참조한다.

#### Common 모듈 참조 (Hard Rule)

**Devian v10에서 생성되는 모든 Module/DATA 도메인 모듈과 Protocol 모듈은 Common 모듈을 무조건 참조해야 한다.**

- 예외: Common 모듈 자기 자신(`Devian.Module.Common`, `@devian/module-common`)은 자기 자신을 참조하지 않는다.
- “참조 판정”은 하지 않는다. 항상 참조한다.

적용 대상:

1) DATA Domain 모듈 (`Devian.Module.{DomainKey}`, `@devian/module-{domainkey}`)
   - `{DomainKey} != Common`인 모든 모듈은 `Devian.Module.Common`을 참조한다.
2) PROTOCOL 모듈 (`Devian.Protocol.{ProtocolName}`, `@devian/network-{protocolgroup}`)
   - 모든 Protocol 모듈은 `Devian.Module.Common`을 참조한다.

참조 방식(정책):

- C#: `.csproj`에 `Devian.Module.Common` ProjectReference를 포함한다.
- C# PROTOCOL 생성물(`*.g.cs`): `using Devian.Module.Common;`을 포함한다.
- TS: `package.json` `dependencies`에 `@devian/module-common`을 포함한다.

#### 필수 개념:

- **Contracts**: JSON 기반 타입/enum 정의
- **Tables**: XLSX 기반 테이블 정의 + 데이터

입력 경로는 input_common.json이 정본이다. 예:

- `domains[Common].contractDir = Common/contracts`
- `domains[Common].tableDir = Common/tables`

**키 변경 (레거시 호환):**
- `contractDir` (새 키), `contractsDir` (레거시, deprecated)
- `tableDir` (새 키), `tablesDir` (레거시, deprecated)
- 레거시 키 사용 시 경고 로그 후 정상 처리

**Optional contracts/tables (SKIP 동작):**

contracts/tables 입력이 없거나 경로가 없거나 파일 매칭이 0개인 경우, Hard Fail이 아닌 SKIP 로그를 출력하고 빌드를 계속한다.

SKIP 조건:
- `contractDir` 또는 `contractFiles`가 미설정
- `contractDir`이 존재하지 않음
- glob 결과가 0개

SKIP되어도 타겟 디렉토리는 clean되어 이전 산출물이 제거된다.

#### Tables (XLSX) 헤더/데이터 규약

- 최소 **4행 헤더**를 가진다.
  - Row 1: 컬럼명
  - Row 2: 타입
  - Row 3: 옵션
  - Row 4: 코멘트(해석하지 않음)
- Row 5부터 데이터
- **Header Stop Rule**: Row1에서 빈 셀을 만나면 그 뒤 컬럼은 무시
- **Data Stop Rule**: PrimaryKey 컬럼이 비면 즉시 중단

옵션 해석 정책:

- **PrimaryKey:**
  - `pk` 옵션만 PrimaryKey로 해석한다.
  - `key:true`, `key` 옵션은 **미지원**이다 (사용 시 빌드 실패).
- **gen:\<EnumName\>:**
  - Reserved가 **아니다** (의미 있는 옵션).
  - `gen:` 옵션이 선언된 컬럼은 **반드시 `pk`여야 한다** (gen 컬럼 = PK 컬럼).
- `optional:true`는 "nullable/optional column" 힌트로만 사용
- 그 외 `parser:*` 등은 **Reserved** (있어도 무시 / 의미 부여 금지)

> 상세 타입 지원/파서 동작/산출 코드 형태는 런타임/제너레이터 코드를 정답으로 본다.

#### DATA 산출물 경로(정책)

- staging:
  - `{tempDir}/{DomainKey}/cs/generated/{DomainKey}.g.cs`
  - `{tempDir}/{DomainKey}/ts/generated/{DomainKey}.g.ts`, `{tempDir}/{DomainKey}/ts/index.ts`
  - `{tempDir}/{DomainKey}/data/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{tempDir}/{DomainKey}/data/pb64/{TableName}.asset` (pk 옵션 있는 테이블만, 내용은 pb64 YAML)
- final (csConfig/tsConfig/dataConfig 기반):
  - `{csConfig.generateDir}/Devian.Module.{DomainKey}/generated/{DomainKey}.g.cs`
  - `{tsConfig.generateDir}/devian-module-{domainkey}/generated/{DomainKey}.g.ts`, `index.ts`
  - `{dataConfig.tableDirs[i]}/{DomainKey}/ndjson/{TableName}.json` (내용은 NDJSON)
  - `{dataConfig.tableDirs[i]}/{DomainKey}/pb64/{TableName}.asset` (pk 옵션 있는 테이블만, 내용은 pb64 YAML)

**Deprecated 필드 (Hard Fail):**
- `domains[*].csTargetDir` — 더 이상 사용하지 않음, `csConfig.generateDir` 사용, 존재 시 빌드 실패
- `domains[*].tsTargetDir` — 더 이상 사용하지 않음, `tsConfig.generateDir` 사용, 존재 시 빌드 실패
- `domains[*].dataTargetDirs` — 더 이상 사용하지 않음, `dataConfig.tableDirs` 사용, 존재 시 빌드 실패

> Domain의 모든 Contract, Table Entity, Table Container는 단일 파일(`{DomainKey}.g.cs`, `{DomainKey}.g.ts`)에 통합 생성된다.
> **파일 확장자는 `.json`이지만, `ndjson/` 폴더의 파일 내용은 NDJSON(라인 단위 JSON)이다.** 확장자는 소비 측(Unity/툴링) 요구로 `.json`을 사용한다.

**Unity Table ID Inspector 소비 규칙 (Hard Rule):**
- Unity Table ID Inspector(EditorID_SelectorBase 기반)는 `ndjson/` 폴더의 `{TableName}.json`(내용은 NDJSON)을 로드한다.
- Inspector 생성물은 반드시 `.json` 확장자 필터를 사용해야 한다.
- `.ndjson` 확장자 필터 사용 시 **정책 위반(FAIL)**.

#### DATA export PK 규칙 (Hard Rule)

**DATA export는 PK 유효 row만 포함하며, 유효 row가 없으면 산출물을 생성하지 않는다.**

- `primaryKey`(pk 옵션)가 정의되지 않은 테이블은 ndjson/pb64 파일을 생성하지 않는다.
- `primaryKey` 값이 비어있는 row는 export 대상에서 제외된다 (ndjson).
- `pb64/` export의 경우: row 중 pk가 빈 값이 하나라도 있으면 **테이블 전체 스킵** (부분 export 금지).
- 결과적으로 유효 row가 0개인 경우 파일을 생성하지 않고 `[Skip]` 로그를 남긴다.

#### pb64 export 규약 (Hard Rule)

**pk 옵션이 있는 테이블만 Unity TextAsset `.asset` 파일로 export한다.**

포맷 (Unity TextAsset YAML):
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!49 &4900000
TextAsset:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: <TABLE_NAME>
  m_Script: <BASE64>
```

- 파일명: `{TableName}.asset` (테이블 단위 1개 파일)
- `m_Name`: 테이블 이름과 동일
- `m_Script`: **DVGB gzip 블록 컨테이너** (base64 인코딩)
  - 헤더: `DVGB` (4바이트) + 버전 1 (1바이트) + blockSize (4바이트) + blockCount (4바이트)
  - 블록: 1024K 단위로 gzip 압축, 각 블록에 uncompressedLen + compressedLen + gzipBytes
  - 압축 전 데이터: 기존 pb64 rawBinary (varint length-delimited JSON rows)
- pk 옵션이 없는 테이블은 export 안함
- row 중 pk가 빈 값이 하나라도 있으면 테이블 전체 스킵
- 하위 호환: C# 로더는 `DVGB` 헤더가 없으면 기존 포맷으로 처리

결정성 요구: 같은 입력이면 항상 같은 .asset 출력

#### C# Namespace (Hard Rule)

DATA Domain 생성물(`{DomainKey}.g.cs`)의 C# 네임스페이스는 **반드시** 아래 규칙을 따른다.

- `namespace Devian.Module.{DomainKey}`

예: DomainKey `Common` → `namespace Devian.Module.Common`

#### TS index.ts Marker 관리 (Hard Rule)

**TS `devian-module-*/index.ts`는 빌더가 관리하되, 통째 덮어쓰기를 금지한다.**

- 빌더는 **marker 구간만 갱신**하며, 나머지 영역은 보존한다.
- marker 구간은 최소 2개:
  - `// <devian:domain-exports>` ~ `// </devian:domain-exports>` — Domain 생성물 export
  - `// <devian:feature-exports>` ~ `// </devian:feature-exports>` — features 폴더 export
- `features/index.ts`도 동일한 marker 방식으로 자동 관리된다.
- 개발자는 marker 안을 **절대 수정하지 않는다**.

> 상세 규칙은 `skills/devian-common/01-module-policy/SKILL.md`를 참조한다.

### 2) DomainType = PROTOCOL

PROTOCOL 입력은 input_common.json의 `protocols` 섹션(배열)이 정의한다.

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

**Deprecated 필드 (Hard Fail):**
- `csTargetDir` — 더 이상 사용하지 않음, `csConfig.generateDir` 사용, 존재 시 빌드 실패
- `tsTargetDir` — 더 이상 사용하지 않음, `tsConfig.generateDir` 사용, 존재 시 빌드 실패
- `upmTargetDir` — 더 이상 사용하지 않음, 사용 시 빌드 실패
- `upmName` — 더 이상 사용하지 않음, 자동 계산됨, 사용 시 빌드 실패

#### Protocol Spec 포맷

- 입력 파일은 **JSON**이며 `protocolDir` 아래 `protocolFiles`에 명시된 파일을 처리한다.
- 파일명 base를 **ProtocolName**으로 간주한다. (예: `C2Game.json` → `C2Game`)

#### Opcode/Tag 레지스트리 (결정성)

- `{ProtocolName}.opcodes.json`, `{ProtocolName}.tags.json`은 **프로토콜 호환성을 위한 Registry**다.
- Registry 파일은 `protocolDir/generated/`에 위치하며, 빌드 시 갱신된다.
- Registry는 "생성된 입력" 파일로, 기계가 생성하지만 입력 폴더에 보존된다.
- 정책 목표:
  - **결정적(deterministic)** 이여야 한다.
  - 명시된 값이 있으면 **명시 값 우선**
  - 미지정 값은 **결정적 규칙으로 자동 할당**
- Tag는 Protobuf 호환 범위를 따르며 **reserved range(19000~19999)**는 금지

> "자동 할당의 정확한 규칙(최소값/정렬/증가 방식)"은 코드를 정답으로 본다.

#### PROTOCOL 산출물 경로(정책)

**C#:**
- staging: `{tempDir}/Devian.Protocol.{ProtocolName}/{ProtocolName}.g.cs`
- final: `{csConfig.generateDir}/Devian.Protocol.{ProtocolName}/{ProtocolName}.g.cs`
- 프로젝트 파일: `{csConfig.generateDir}/Devian.Protocol.{ProtocolName}/Devian.Protocol.{ProtocolName}.csproj`
- namespace: `Devian.Protocol.{ProtocolName}` (변경 금지)

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/{ProtocolName}.g.ts`, `index.ts`
- final: `{tsConfig.generateDir}/devian-network-{protocolgroup}/{ProtocolName}.g.ts`, `index.ts`

> **생성물 namespace 고정 (Hard Rule):**
> C# 생성물 namespace는 `Devian.Protocol.{ProtocolName}`으로 고정이며, 런타임 모듈 단일화와 무관하게 변경하지 않는다.

#### Protocol UPM 자동 생성 규칙 (Hard Rule)

**Protocol UPM 패키지는 항상 자동 생성된다.** (옵션/토글 없음)

**UPM 패키지명 자동 계산:**
```
computedUpmName = "com.devian.protocol." + normalize(group)
```

**normalize(group) 규칙:**
1. 소문자화
2. 공백은 `_`(underscore)로 치환
3. `_`(underscore)는 유지 ← **중요**
4. 허용 문자: `[a-z0-9._-]`만 유지, 그 외 제거
5. 앞/뒤의 `.`, `-`, `_` 정리

**예시:**
| group | computedUpmName |
|-------|-----------------|
| `Game` | `com.devian.protocol.game` |
| `Game_Server` | `com.devian.protocol.game_server` |
| `Auth Service` | `com.devian.protocol.auth_service` |

**경로 계산:**
```
stagingDir = {tempDir}/Devian.Protocol.{ProtocolName}-upm
targetDir = {upmConfig.sourceDir}/{computedUpmName}
finalDir = {upmConfig.packageDir}/{computedUpmName}
```

**충돌 정책 (Hard Fail):**
1. `upm`에 동일한 `computedUpmName`이 존재하면 빌드 **FAIL**
2. `upm`에 동일한 `computedUpmName`이 이미 존재하면 빌드 **FAIL** (중복 생성)
3. `protocols` 배열 내에서 동일한 `computedUpmName`이 계산되면 빌드 **FAIL**

> 덮어쓰기/우선순위 없음. 모든 충돌은 명시적 오류.

---

## Unity C# Compatibility Gate (Hard Rule)

**Unity C# 문법 제한은 `skills/devian/04-unity-csharp-compat/SKILL.md`가 정본이다.**

### DoD (완료 정의) — 하드 게이트

아래 패턴이 적용 범위 경로에서 **1개라도 발견되면 FAIL**:

| 금지 패턴 (정규식) | 탐지 대상 |
|-------------------|-----------|
| `\bclass\s+\w+\s*\(` | class primary constructor |
| `\bstruct\s+\w+\s*\(` | struct primary constructor |
| `\brecord\b` | record 타입 |
| `\brequired\b` | required 멤버 |
| `^\s*namespace\s+.*;\s*$` | file-scoped namespace |
| `\bglobal\s+using\b` | global using |
| `\bnew\s*\(\s*\)\s*;?` | target-typed new |
| `\binit\s*;` | init accessor |
| `\bdelegate\b[^{;]*\w+\s*\?\s*\(` | delegate 식별자에 `?` 붙은 경우 |
| `\b[A-Za-z_][A-Za-z0-9_]*\s*\?\?\b` | 타입/선언부에서 `??` 패턴 |

**검사 대상 경로:**
- `framework-cs/upm/`
- `framework-cs/apps/**/Packages/`
- UPM 패키지 내부의 `Samples~/` 및 템플릿/샘플 코드도 검사 대상에 포함한다.

> 이 게이트는 "정책 위반이 아니더라도 깨지는 코드"를 잡는 최소 장치다.
> 상세 규칙은 04 스킬을 참조한다.

---

## Table ID Inspector 생성물 Gate (Hard Rule)

**Table ID Inspector 생성물은 `.json` 확장자 필터를 사용해야 한다.**

### DoD (완료 정의) — 하드 게이트

검사 대상 경로:
- `framework-cs/upm/**/Editor/Generated/*.cs`
- `framework-cs/apps/**/Packages/**/Editor/Generated/*.cs`

**Hard FAIL:**
- 위 대상에서 문자열 `".ndjson"` 발견 시 **FAIL**
- 정본: `.EndsWith(".json"` 형태여야 함

> DATA 파일 확장자는 `.json`(내용은 NDJSON)이 정본이다.
> Inspector가 `.ndjson`을 검색하면 파일을 찾지 못한다.

---

## Hard Conflicts (DoD)

아래는 발견 즉시 FAIL(반드시 0개)로 취급한다.

1. 입력 포맷이 서로 다르게 서술됨 (예: PROTOCOL이 .proto/IDL이라고 서술)
2. opcode/tag 규칙이 비결정적으로 서술됨 (재배정/랜덤/비결정 허용)
3. input_common.json과 경로/플레이스홀더 규약이 불일치
4. Reserved 옵션(`parser:*` 등)을 강제/필수/의미로 서술
5. 코드와 다른 API/산출물/프레임 규약을 SKILL이 "정본"처럼 단정
6. TS `index.ts`를 통째로 덮어쓰는 동작 (marker 갱신 방식 위반)
7. `domains.Common`이 input_common.json에 없는 상태

---

## Soft Conflicts (충돌 아님)

- 용어/표기/톤 차이
- 문서 링크가 끊김

단, Soft가 Hard 오해를 유발하면 개선 대상이다.

---

## 공식 빌드 러너

공식 빌드 실행은 Node 기반 빌더를 사용한다: `framework-ts/tools/builder/build.js`

C# 모듈은 레포에 `.csproj`/`.sln`을 포함하여 dotnet 빌드 및 IDE를 정식 지원한다.

---

## Reference

- **정책 정본:** 이 문서 (`skills/devian/03-ssot/SKILL.md`)
- **빌드 스키마 정본:** `input_common.json`
- **동작 정본:** 런타임/제너레이터 코드
