# Devian – 68 Migration Checklist

## Purpose

**build.json v9 빌드 검증을 위한 체크리스트를 제공한다.**

---

## Belongs To

**Tooling / Build**

---

## Phase 계획

### Phase 1 (완료)

- [x] build.json v7로 변환 (키 rename + files 패턴)
- [x] Common/Content 숫자 prefix 폴더 적용 (1.contracts, 2.tables, 3.protocols)
- [x] 스킬 문서 v7 스키마 반영

### Phase 2 (완료)

- [x] Devian.Tools에서 v7 스키마 읽기 구현
- [x] Dir + Files glob 패턴으로 파일 수집
- [x] 파일 목록 정렬/중복 제거 (빌드 재현성)
- [x] v7 강제 + deprecated 키 감지 + 필수 필드 검증
- [x] `.xlsx`/`.proto` 입력 시 NotSupportedException 처리

### Phase 3 (완료)

- [x] `.xlsx` 테이블 파싱 구현 (ClosedXML, 4행 헤더)
- [x] `.proto` JSON(proto-json) 파싱 시도 구현
- [x] Protobuf IDL .proto 미지원 에러 처리

### Phase 4 (완료)

- [x] build.json v8 스키마 적용
- [x] 숫자 prefix 폴더 제거 (1.contracts → contracts 등)
- [x] 섹션 배타 규칙 적용 (protocol-only vs data domain)
- [x] protocolFile 단수 변경 (protocolFiles → protocolFile)
- [x] 레거시 키 validate fail 처리

### Phase 4.5 (완료)

- [x] build.json v9 스키마 적용
- [x] domains/protocols 분리 (v8 혼합 제거)
- [x] protocol-only domain → protocols 섹션으로 이동
- [x] Devian.Tools 모델: `BuildConfig.Protocols` 추가
- [x] 스킬 문서 v9 정본화

### Phase 5 (예정)

- [ ] Protobuf IDL .proto 파싱 (protoc 연동)

**Devian.Tools는 v9 스키마를 지원한다. Protobuf IDL .proto 파싱은 Phase 5에서 지원 예정.**

---

## v9 스키마 요약

**build.json v9에서 domains와 protocols는 목적이 다르며 섞이지 않는다.**

| 항목 | 규칙 |
|------|------|
| Build 단위 | Domain / Protocol |
| domains | tables + contracts (Data) |
| protocols | protocolsDir + protocolFile (IDL) |
| 생성 위치 | `tempDir/{name}/{cs\|ts\|data}` |

---

## build.json 스펙 (v9)

### 최상위 구조

```json
{
  "version": "9",
  "tempDir": "temp/devian",
  "domains": { ... },
  "protocols": { ... }
}
```

### domains 구조 (Common 예시)

```json
{
  "contractsDir": "input/Common/contracts",
  "contractFiles": ["*.json"],
  "tablesDir": "input/Common/tables",
  "tableFiles": ["*.xlsx"],

  "csTargetDirs": ["modules/cs/Common"],
  "tsTargetDirs": ["modules/ts/Common"],
  "dataTargetDirs": ["modules/data/Common"]
}
```

### protocols 구조 (C2Game 예시)

```json
{
  "protocolsDir": "input/C2Game/protocols",
  "protocolFile": "C2Game.proto",

  "csTargetDirs": ["modules/cs/C2Game"],
  "tsTargetDirs": ["modules/ts/C2Game"]
}
```

> ⚠️ `*TargetDirs`는 **모듈 루트**만 지정. 생성물 하위 경로(`Runtime/generated` 등)는 Devian이 자동 결정.

---

## Validator 체크 항목 (빌드 초기 수행)

### 1. Config 구조 검증 (strict)

| 검증 | 설명 |
|------|------|
| version = "9" | 필수 |
| domains 분리 | tables + contracts만 허용 |
| protocols 분리 | protocolsDir + protocolFile만 허용 |
| Data domain 필수 | tables 또는 contracts 중 최소 하나 |

### 2. 레거시 키 감지 (FAIL)

| 레거시 키 | 대체 키 |
|----------|---------|
| `tableDir` | `tablesDir` |
| `contractDir` | `contractsDir` |
| `protocolDir` | `protocolsDir` |
| `protocolFiles` | `protocolFile` (단수) |
| domains 안 `protocolsDir/protocolFile` | protocols 섹션으로 이동 |

### 3. 입력 디렉터리 존재 검증

```
각 domain/protocol에 대해:
- contractsDir가 지정되었는데 디렉터리가 없으면 → 경고
- tablesDir가 지정되었는데 디렉터리가 없으면 → 경고
- protocolsDir가 지정되었는데 디렉터리가 없으면 → 경고
```

### 3. Protocol namespace 검증

| 검증 | 설명 |
|------|------|
| 파일명 기반 namespace | 강제 |
| JSON namespace 존재 시 | 파일명과 완전 일치해야 함 |
| namespace 충돌 | 같은 namespace 파일 2개 발견 시 실패 |

### 4. 산출물-로더 정합성 검증 (테이블)

```
Table.*.g.cs가 요구하는 키 목록과
data 출력 디렉토리에 실제 JSON 존재 검사
→ 누락되면 실패 (경고로 넘기지 말 것)
```

---

## Protocol 마이그레이션

### 파일명 = namespace 규칙

| Before | After |
|--------|-------|
| `ws/messages.json` | `ws.json` |
| `net/messages.json` | `C2Game.json`, `Game2C.json` |

### JSON 내부 namespace 처리

| 상황 | 처리 |
|------|------|
| 필드 없음 (권장) | 파일명을 namespace로 사용 |
| 필드 있음 | 파일명과 일치 검증, 불일치 시 실패 |

### 구현 코드

```csharp
var filenameBase = Path.GetFileNameWithoutExtension(filePath);

if (spec.Namespace != null && spec.Namespace != filenameBase)
{
    throw new BuildException($"namespace mismatch: file={filenameBase}, json={spec.Namespace}");
}

spec.Namespace = filenameBase; // 강제 적용
```

---

## 생성/복사 규칙

### 생성 위치 (항상 tempDir)

```
{tempDir}/{domain}/cs/    ← C# 생성물
{tempDir}/{domain}/ts/    ← TS 생성물
{tempDir}/{domain}/data/  ← JSON 데이터
```

### 복사 규칙

```
source: {tempDir}/{domain}/{cs|ts|data}/
target: 각 {cs|ts|data}TargetDirs[]

primary 개념: ❌
from/to DSL: ❌
→ 항상 temp → targetDirs
```

---

## CLI 커맨드

```bash
devian build input/build/build.json

# 옵션
--domain <n>    도메인 단위 실행
--clean-temp       tempDir/{name} 삭제 후 빌드
--clean-targets    copy 전에 targetDirs 삭제
```

---

## 마이그레이션 순서 (v9 기준)

| 순서 | 작업 |
|------|------|
| 1 | build.json v9 스펙 반영 (domains/protocols 분리) |
| 2 | protocol-only domain → protocols 섹션으로 이동 |
| 3 | Devian.Tools에서 `Protocols` 모델 추가 |
| 4 | domains/protocols 루프 분리 처리 |
| 5 | 레거시 키 validate fail 처리 (v8 혼합 포함) |
| 6 | 스킬 문서 v9 정본화 |
| 7 | `devian build`로 clean build 재현 가능 확인 |

---

## Acceptance Criteria (v9)

| # | 조건 |
|---|------|
| 1 | v9 필수 필드 누락 시 빌드 실패 |
| 2 | domains에는 tables/contracts만 허용 |
| 3 | protocols에는 protocolsDir/protocolFile만 허용 |
| 4 | domains 안에 protocolsDir/protocolFile이 있으면 빌드 실패 (v8 혼합) |
| 5 | Protocol namespace는 파일명에서 결정됨 |
| 6 | clean build 재현 가능 |
| 7 | 각 domain/protocol moduleRoot에 scaffold 파일 존재 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `62-protocolgen-implementation` | Protocol 생성 |
| `63-build-runner` | 빌드 실행 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 3.0.0 | 2025-12-28 | v9 스키마: domains/protocols 분리 |
| 2.0.0 | 2025-12-28 | v8 스키마: 섹션 배타 규칙, protocolFile 단수 |
| 1.3.0 | 2025-12-28 | Phase 3 완료: xlsx/proto(JSON) 지원 |
| 1.2.0 | 2025-12-28 | Phase 2 완료 (v7 스키마 지원) |
| 1.1.0 | 2025-12-28 | v7 스키마 + Phase 계획 |
| 1.0.0 | 2025-12-28 | Initial |
