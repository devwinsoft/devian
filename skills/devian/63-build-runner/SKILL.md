# Devian – 63 Build Runner

## Purpose

**`build.json`을 파싱하여 domain 단위로 generator를 실행하고, tempDir에서 생성 후 targetDirs로 복사한다.**

"쉘 1방 빌드"의 **엔진**.

---

## Belongs To

**Tooling**

---

## Build 철학 (확정)

| 원칙 | 설명 |
|------|------|
| **Build 단위는 Domain** | domain 정보만 있으면 build가 가능해야 한다 |
| **생성은 tempDir에서만** | 모든 generated 파일은 `tempDir/{domain}/...`에 생성 |
| **TargetDirs는 모듈 루트** | `*TargetDirs`는 생성 위치가 아니라 모듈 루트 |
| **고정 하위 폴더로만 write** | Target Type별 고정된 하위 디렉터리 규칙을 따름 |

**Devian에서 `*TargetDirs`는 생성 파일이 떨어지는 위치가 아니라 모듈 루트를 의미하며, 생성 파일의 실제 위치는 Target Type별로 고정된 하위 디렉터리 규칙을 따른다.**

### Target Type별 출력 경로 (고정)

| Target | 모듈 루트 | 생성 파일 위치 |
|--------|----------|---------------|
| `cs` | `{csTargetDir}` | `{csTargetDir}/generated/**` |
| `ts` | `{tsTargetDir}` | `{tsTargetDir}/generated/**` |
| `upm` | `{upmTargetDir}` | `{upmTargetDir}/Runtime/**` |
| `data` | `{dataTargetDir}` | `{dataTargetDir}/json/**` |

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| build.json 스키마 파싱/검증 | domains, tempDir |
| 도메인별 실행 계획 수립 | domain 설정 기반 |
| temp workspace 관리 | `tempDir/{domain}/` |
| 단계 실행 | validate → generate (temp) → copy (targetDirs) |

### Out of Scope

| 항목 | 설명 |
|------|------|
| CI 설정 (GitHub Actions 등) | ❌ 별도 |
| 배포/패키징 (npm/nuget publish) | ❌ 별도 |

---

## Inputs

| Input | 설명 |
|-------|------|
| `build.json` | 빌드 설정 (v9) |
| `{domains[name].contractsDir}/` | 계약 원천 (contractFiles 패턴) |
| `{domains[name].tablesDir}/` | 테이블 원천 (tableFiles 패턴) |
| `{protocols[name].protocolsDir}/{protocolFile}` | 프로토콜 원천 (단일 파일) |

**v9 입력 수집 규칙:**
- **domains**: `contractsDir` + `contractFiles`, `tablesDir` + `tableFiles` 패턴으로 glob 수집
- **protocols**: `protocolsDir` + `protocolFile`로 단일 파일 resolve (glob 미사용)

---

## Outputs

| Output | 설명 |
|--------|------|
| `{tempDir}/{domain}/cs/` | C# 생성물 |
| `{tempDir}/{domain}/ts/` | TS 생성물 |
| `{tempDir}/{domain}/data/` | JSON 데이터 |
| `{tempDir}/meta/{domain}/` | Table Meta 등 |
| 복사 대상 | `csTargetDirs`, `tsTargetDirs`, `dataTargetDirs` |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | **Build 단위는 Domain** — domain 정보만으로 빌드 가능 |
| 2 | **모든 생성은 tempDir/{domain}/에서** |
| 3 | **TargetDirs는 모듈 루트** — 생성 위치 아님 |
| 4 | **고정 하위 폴더로만 write** — Target Type별 규칙 준수 |
| 5 | **Protocol namespace는 파일명** — JSON 내부 `"namespace"`는 검증용 |
| 6 | 실패 시 **즉시 종료** (non-zero exit) |
| 7 | 같은 input이면 같은 output (**결정적 빌드**) |
| 8 | **Table API는 `namespace Devian.Table`에 `TB_<TableName>` 타입으로 생성** |
| 9 | **targetDir 자체에 파일을 쓰지 않는다** |

---

## 절대 금지 사항 (MUST NOT)

| # | 금지 항목 |
|---|----------|
| 1 | `targets` / `output` / `variables` 구조 사용 |
| 2 | primary output을 JSON에서 읽기 |
| 3 | protocol namespace를 폴더 기준으로 해석 |
| 4 | domain 외부 경로 참조 (`../`, `net/*.json` 등) |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 실패 시 **어떤 단계/도메인**에서 실패했는지 명확히 출력 |
| 2 | `--dry-run` 옵션으로 계획만 출력 가능 |

---

## Build Pipeline (v9)

```
1. validate-specs
   └── build.json v9, domains/protocols 분리 검증

2. foreach domains[name]:
   │
   ├── 2a. contractgen (64)
   │   └── contractsDir + contractFiles 패턴 → tempDir/{name}/cs|ts
   │
   └── 2b. tablegen (61)
       ├── tablesDir + tableFiles 패턴 → tempDir/{name}/cs|ts (Table API)
       └── tablesDir + tableFiles 패턴 → tempDir/{name}/data (JSON)

3. foreach protocols[name]:
   │
   └── 3a. protocolgen (62)
       └── protocolsDir + protocolFile (단일 파일) → tempDir/{name}/cs|ts
       └── namespace = 파일명

4. copy (고정 하위 폴더로)
   ├── tempDir/{name}/cs → {csTargetDir}/generated/
   ├── tempDir/{name}/ts → {tsTargetDir}/generated/
   ├── tempDir/{name}/data → {dataTargetDir}/json/
   └── (UPM) tempDir/{name}/cs → {upmTargetDir}/Runtime/

5. (optional) build.lock.json
```

---

## Execution Plan Algorithm (v9)

```
1. build.json 로드
2. version="9" 및 domains/protocols 분리 검증
3. domains 순회 (tables + contracts)
4. 각 domain에서:
   - TargetDir(모듈 루트) 존재 보장
   - Target Type별 모듈 스캐폴드 보장
   - contractsDir에서 contractFiles 패턴 매칭 → contractgen
   - tablesDir에서 tableFiles 패턴 매칭 → tablegen
5. protocols 순회 (IDL)
6. 각 protocol에서:
   - protocolsDir + protocolFile → protocolgen (namespace = 파일명)
7. tempDir/{name}/* 생성 완료 후
8. 고정 하위 폴더로 복사:
   - CS: {csTargetDir}/generated/
   - TS: {tsTargetDir}/generated/
   - UPM: {upmTargetDir}/Runtime/
   - DATA: {dataTargetDir}/json/
```

---

## Domain/Protocol 처리 규칙

### 파일 탐색 (v9)

**domains (Data):**
```
{domains[name].contractsDir}/ + contractFiles 패턴
{domains[name].tablesDir}/ + tableFiles 패턴
```

**protocols (IDL):**
```
{protocols[name].protocolsDir}/{protocolFile}  (단일 파일)
```

### Generator 실행 조건

| Generator | 조건 |
|-----------|------|
| contractgen | `domains[name].contractsDir`에서 `contractFiles` 패턴 매칭 파일 존재 |
| tablegen | `domains[name].tablesDir`에서 `tableFiles` 패턴 매칭 파일 존재 |
| protocolgen | `protocols[name].protocolsDir` + `protocolFile` 존재 |

### 복사 조건 및 경로

| 타입 | 조건 | 출력 경로 |
|------|------|----------|
| cs | `csTargetDirs` 비어있지 않음 | `{csTargetDir}/generated/` |
| ts | `tsTargetDirs` 비어있지 않음 | `{tsTargetDir}/generated/` |
| upm | `upmTargetDirs` 비어있지 않음 | `{upmTargetDir}/Runtime/` |
| data | `dataTargetDirs` 비어있지 않음 | `{dataTargetDir}/json/` |

---

## Protocol 처리 규칙 (핵심)

### Namespace 결정

```csharp
var filenameBase = Path.GetFileNameWithoutExtension(filePath);
// filenameBase가 namespace가 된다
```

### JSON "namespace" 검증

```csharp
if (spec.Namespace != null && spec.Namespace != filenameBase)
{
    throw new BuildException($"namespace mismatch: file={filenameBase}, json={spec.Namespace}");
}
spec.Namespace = filenameBase; // 강제 적용
```

---

## 출력 규칙 (Table API)

| 항목 | 규칙 |
|------|------|
| **생성 위치** | `tempDir/{domain}/cs/` |
| **Namespace** | `Devian.Table` |
| **파일명** | `Table.g.cs` |
| **타입** | `public static class TB_{TableName}` |

---

## build.json Validation (v9)

| 검증 항목 | 설명 |
|----------|------|
| version | `"9"` 필수 |
| required | `tempDir`, `domains` 또는 `protocols` 중 최소 하나 |
| domains | tables 또는 contracts 중 최소 하나 필수 |
| protocols | `protocolsDir` + `protocolFile` 둘 다 필수 |

**v9 스키마:**
- **domains**: `contractsDir`/`contractFiles`, `tablesDir`/`tableFiles`
- **protocols**: `protocolsDir`/`protocolFile` (단수)

**deprecated 키 감지 시 빌드 실패:**
- `tableDir`, `contractDir`, `protocolDir` (단수형 - v7 레거시)
- `protocolFiles` (복수형 - v7 레거시)
- domains 안에 `protocolsDir`/`protocolFile` (v8 혼합)

---

## CLI Interface (권장)

| Command | 설명 |
|---------|------|
| `devian build` | 전체 빌드 |
| `devian build --domain net` | 단일 도메인 |
| `devian build --dry-run` | 계획만 출력 |

---

## Responsibilities

Runner는 다음 순서만 수행한다:

1. **build.json 파싱 및 검증**
2. **TargetDir(모듈 루트) 존재 보장**
3. **Target Type에 맞는 모듈 스캐폴드 보장**
   - CS: C# Class Library 스캐폴드
   - TS: NodeJS 라이브러리 스캐폴드
   - UPM: Unity Package 스캐폴드
4. **generator 호출 (tempDir에 생성)**
5. **고정된 하위 폴더로만 write**
   - CS → `{csTargetDir}/generated/`
   - TS → `{tsTargetDir}/generated/`
   - UPM → `{upmTargetDir}/Runtime/`
   - DATA → `{dataTargetDir}/json/`
6. **temp workspace 관리**
7. **(옵션) Common 빌드 결과 → Devian.Common / devian-common 모듈 동기화**
   - C# 생성물 → `framework/cs/Devian.Common/generated/`
   - TS 생성물 → `framework/ts/devian-common/generated/`
   - manual 폴더는 생성기가 덮어쓰지 않음

### Runner 금지 규칙

| # | 금지 |
|---|------|
| 1 | targetDir 자체에 파일을 쓰지 않는다 |
| 2 | Target Type별 출력 경로를 임의로 변경하지 않는다 |
| 3 | 생성기 로직을 변경하지 않는다 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | **domain/protocol 정보만으로** 해당 빌드 가능 |
| 2 | 모든 생성은 `tempDir/{name}/`에서 이루어짐 |
| 3 | `*TargetDirs`는 **모듈 루트**로 해석됨 |
| 4 | CS 생성물은 `{csTargetDir}/generated/`에만 생성됨 |
| 5 | TS 생성물은 `{tsTargetDir}/generated/`에만 생성됨 |
| 6 | UPM 생성물은 `{upmTargetDir}/Runtime/`에만 생성됨 |
| 7 | DATA JSON은 `{dataTargetDir}/json/`에만 생성됨 |
| 8 | **Protocol namespace는 파일명**에서 결정됨 |
| 9 | 실패 시 **어떤 단계/도메인**에서 실패했는지 출력 |
| 11 | Table API는 **`namespace Devian.Table`에 `TB_<TableName>` 타입**으로 생성됨 |
| 12 | **targetDir 자체에 파일이 직접 생성되지 않음** |
| 13 | **생성 코드는 블록 네임스페이스 사용** — `namespace X { }` (C# 9 호환) |

---

## 한 줄 요약

> **Build Runner는 domains(data)/protocols(IDL) 분리 처리하여 tempDir에 생성하고, Target Type별 고정 하위 폴더로 복사한다.**

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `61-tablegen-implementation` | 테이블 생성기 |
| `62-protocolgen-implementation` | 프로토콜 생성기 |
| `64-contractgen-implementation` | 컨트랙트 생성기 |
| `00-rules-minimal` | Hard Rules |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 3.0.0 | 2025-12-28 | v9 스키마: domains/protocols 분리 |
| 2.0.0 | 2025-12-28 | v8 스키마: 섹션 배타 규칙, protocolFile 단수 |
| 1.3.0 | 2025-12-28 | Phase 3 완료: xlsx/proto(JSON) 지원 |
| 1.2.0 | 2025-12-28 | v7 스키마 지원 (Phase 2 완료) |
| 1.1.0 | 2025-12-28 | v7 스키마 (Dir + Files 패턴) |
| 1.0.0 | 2025-12-28 | Initial |
