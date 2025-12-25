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
| **outputs는 복사 대상만** | `*TargetDirs`는 생성 위치가 아니라 복사 대상 |

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| build.json 스키마 파싱/검증 | inputDirs, tempDir, domains |
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
| `build.json` | 빌드 설정 |
| `{inputDirs.contractsDir}/{domain}/` | 계약 원천 |
| `{inputDirs.protocolsDir}/{domain}/` | IDL 원천 |
| `{inputDirs.tablesDir}/{domain}/` | 테이블 원천 |

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
| 3 | **outputs(targetDirs)는 복사 대상만** — 생성 위치 아님 |
| 4 | **Protocol namespace는 파일명** — JSON 내부 `"namespace"`는 검증용 |
| 5 | 실패 시 **즉시 종료** (non-zero exit) |
| 6 | 같은 input이면 같은 output (**결정적 빌드**) |
| 7 | **Loader namespace는 `Devian.Tables` 고정** |

---

## 절대 금지 사항 (MUST NOT)

| # | 금지 항목 |
|---|----------|
| 1 | `targets` / `output` / `variables` 구조 사용 |
| 2 | primary output을 JSON에서 읽기 |
| 3 | protocol namespace를 폴더 기준으로 해석 |
| 4 | domain 외부 경로 참조 (`../`, `net/*.json` 등) |
| 5 | `protocolNamespaces` 사용 (폐기됨) |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 실패 시 **어떤 단계/도메인**에서 실패했는지 명확히 출력 |
| 2 | `--dry-run` 옵션으로 계획만 출력 가능 |

---

## Build Pipeline (확정)

```
1. validate-specs
   └── build.json, inputDirs 경로 검증

2. foreach domain:
   │
   ├── 2a. contractgen (64)
   │   └── contractsFiles → tempDir/{domain}/cs|ts
   │
   ├── 2b. protocolgen (62)
   │   └── protocolFiles → tempDir/{domain}/cs|ts
   │   └── namespace = 파일명
   │
   ├── 2c. tablegen (61)
   │   ├── tablesFiles → tempDir/{domain}/data
   │   └── Table Meta 생성 (tempDir/meta/{domain}/)
   │
   ├── 2d. table-loader-codegen (67)
   │   └── Table Meta → tempDir/{domain}/cs/Table.{ExcelFileName}.g.cs
   │
   └── 2e. copy
       ├── tempDir/{domain}/cs → csTargetDirs[]
       ├── tempDir/{domain}/ts → tsTargetDirs[]
       └── tempDir/{domain}/data → dataTargetDirs[]

3. (optional) build.lock.json
```

---

## Execution Plan Algorithm

```
1. build.json 로드
2. inputDirs, tempDir 검증
3. domains 순회
4. 각 domain에서:
   - contractsFiles 있으면 → contractgen
   - protocolFiles 있으면 → protocolgen (namespace = 파일명)
   - tablesFiles 있으면 → tablegen + table-loader-codegen
5. tempDir/{domain}/* 생성 완료 후
6. *TargetDirs로 복사 (비어있으면 해당 타입 스킵)
```

---

## Domain 처리 규칙

### 파일 탐색

```
{inputDirs.contractsDir}/{domain}/{file}
{inputDirs.protocolsDir}/{domain}/{file}
{inputDirs.tablesDir}/{domain}/{file}
```

### Generator 실행 조건

| Generator | 조건 |
|-----------|------|
| contractgen | `contractsFiles` 비어있지 않음 |
| protocolgen | `protocolFiles` 비어있지 않음 |
| tablegen | `tablesFiles` 비어있지 않음 |
| table-loader-codegen | `tablesFiles` + `csTargetDirs` 모두 비어있지 않음 |

### 복사 조건

| 타입 | 조건 |
|------|------|
| cs | `csTargetDirs` 비어있지 않음 |
| ts | `tsTargetDirs` 비어있지 않음 |
| data | `dataTargetDirs` 비어있지 않음 |

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

## 출력 규칙 (Table Loader)

| 항목 | 규칙 |
|------|------|
| **생성 위치** | `tempDir/{domain}/cs/` |
| **Namespace** | `Devian.Tables` **고정** |
| **파일명** | `Table.{ExcelFileName}.g.cs` |
| **클래스** | `partial class Table` + `T_{TableName}` |

---

## build.json Validation

| 검증 항목 | 설명 |
|----------|------|
| required | `inputDirs`, `tempDir`, `domains` |
| inputDirs | `contractsDir`, `tablesDir`, `protocolsDir` 존재 확인 |
| domains | 각 domain의 `*Files`, `*TargetDirs` 형식 확인 |

### 폐기된 필드 검출

```csharp
// 폐기된 필드 사용 시 즉시 실패
if (config.Contains("targets") || config.Contains("variables") || config.Contains("inputs"))
{
    throw new BuildException("Deprecated fields detected. Use inputDirs, tempDir, domains only.");
}
```

---

## CLI Interface (권장)

| Command | 설명 |
|---------|------|
| `devian build` | 전체 빌드 |
| `devian build --domain net` | 단일 도메인 |
| `devian build --dry-run` | 계획만 출력 |

---

## Responsibilities

1. **build.json 파싱 및 검증**
2. **도메인별 실행 계획 수립**
3. **generator 호출 (tempDir에 생성)**
4. **targetDirs로 복사**
5. **temp workspace 관리**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | **domain 정보만으로** 해당 domain 빌드 가능 |
| 2 | 모든 생성은 `tempDir/{domain}/`에서 이루어짐 |
| 3 | `*TargetDirs`가 비어있으면 해당 타입 복사 스킵 |
| 4 | **Protocol namespace는 파일명**에서 결정됨 |
| 5 | 실패 시 **어떤 단계/도메인**에서 실패했는지 출력 |
| 6 | 폐기된 필드(`targets`, `variables`, `inputs`) 사용 시 즉시 실패 |
| 7 | Loader 코드의 **namespace는 `Devian.Tables`** |

---

## 한 줄 요약

> **Build Runner는 domain 단위로 tempDir에 생성하고, targetDirs로 복사한다.**

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `61-tablegen-implementation` | 테이블 생성기 |
| `62-protocolgen-implementation` | 프로토콜 생성기 |
| `64-contractgen-implementation` | 컨트랙트 생성기 |
| `67-table-loader-codegen` | loader 코드 생성 |
| `00-rules-minimal` | Hard Rules |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-25 | **build.json 전면 재설계 반영**: inputDirs, tempDir, targetDirs, protocolNamespaces 폐기 |
| 0.2.0 | 2024-12-21 | table-loader-codegen(67) 단계 추가 |
| 0.1.0 | 2024-12-21 | Initial skill definition |
