# Devian – 60 Build Pipeline

## Purpose

**Devian 빌드 파이프라인 스펙을 정의한다.**

---

## Domain Root (정본)

**Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다.**

모든 Domain의 입력 경로는 build.json의 도메인별 설정에서 명시된다.

### v9 스키마 (정본)

**build.json v9에서 `domains`와 `protocols`는 목적이 다르며 섞이지 않는다.**

**domains (Data Domain - tables + contracts):**
```
domains[name].contractsDir  → contracts 디렉터리
domains[name].contractFiles → contracts 파일 패턴 (예: ["*.json"])
domains[name].tablesDir     → tables 디렉터리
domains[name].tableFiles    → tables 파일 패턴 (예: ["*.xlsx"])
```

**protocols (IDL link - protocolsDir + protocolFile):**
```
protocols[name].protocolsDir  → protocols 디렉터리
protocols[name].protocolFile  → protocol 파일명 (단수, 예: "C2Game.proto")
```

**protocolFile은 단일 파일명이고 `{protocolsDir}/{protocolFile}`로 resolve한다.**

`domains/` 디렉터리는 Devian 구조에 존재하지 않는다.
`{Name}` 표기는 문서/설명용 플레이스홀더이며,
실제 파일 시스템 경로를 의미하지 않는다.

---

## Belongs To

**Build / Core**

---

## 1. 입력 폴더 규칙 (v9)

**입력 폴더는 숫자 prefix 없이 `input/{Name}/tables|contracts|protocols`를 사용한다.**

입력 디렉토리 구조:

```
input/
├── {DataDomain}/
│   ├── contracts/   ← *.json (contractFiles 패턴)
│   └── tables/      ← *.xlsx (tableFiles 패턴)
├── {ProtocolName}/
│   └── protocols/
│       └── {ProtocolFile}.proto  ← 단일 파일
└── build/
    └── build.json
```

예시:

```
input/
├── Common/               ← domains (Data domain)
│   ├── contracts/
│   │   └── TestContract.json
│   └── tables/
│       └── TestTable.xlsx
├── C2Game/               ← protocols (IDL)
│   └── protocols/
│       └── C2Game.proto
├── Game2C/               ← protocols (IDL)
│   └── protocols/
│       └── Game2C.proto
└── build/
    └── build.json
```

### 입력 탐색 규칙

**Data Domain:**
- `contractsDir` 아래에서 `contractFiles` 패턴으로 입력을 찾는다
- `tablesDir` 아래에서 `tableFiles` 패턴으로 입력을 찾는다

**Protocol-only Domain:**
- `protocols` 섹션에서 `protocolsDir` + `protocolFile`로 단일 파일을 resolve한다
- glob 패턴 미사용

---

## 2. build.json 스펙 (v9)

**build.json v9에서 domains와 protocols는 목적이 다르며 섞이지 않는다.**

```json
{
  "version": "9",
  "tempDir": "temp/devian",

  "domains": {
    "Common": {
      "contractsDir": "input/Common/contracts",
      "contractFiles": ["*.json"],

      "tablesDir": "input/Common/tables",
      "tableFiles": ["*.xlsx"],

      "csTargetDirs": ["modules/cs/Common"],
      "tsTargetDirs": ["modules/ts/Common"],
      "dataTargetDirs": ["modules/data/Common"]
    }
  },

  "protocols": {
    "C2Game": {
      "protocolsDir": "input/C2Game/protocols",
      "protocolFile": "C2Game.proto",

      "csTargetDirs": ["modules/cs/C2Game"],
      "tsTargetDirs": ["modules/ts/C2Game"]
    },

    "Game2C": {
      "protocolsDir": "input/Game2C/protocols",
      "protocolFile": "Game2C.proto",

      "csTargetDirs": ["modules/cs/Game2C"],
      "tsTargetDirs": ["modules/ts/Game2C"]
    }
  }
}
```

**v9 핵심 규칙:**
- `domains`: tables + contracts만 (Data domain)
- `protocols`: protocolsDir + protocolFile만 (IDL link domain)
- `protocolFile`은 단일 파일명이고 `{protocolsDir}/{protocolFile}`로 resolve한다
- 입력 폴더는 숫자 prefix 없이 `input/{Name}/tables|contracts|protocols`를 사용한다

---

## 3. TargetDirs 정의 (단일 진실)

**Devian에서 `*TargetDirs`는 생성 파일이 떨어지는 위치가 아니라 모듈 루트를 의미하며, 생성 파일의 실제 위치는 Target Type별로 고정된 하위 디렉터리 규칙을 따른다.**

### Target Type별 모듈 형태

| Target | 모듈 형태 |
|--------|----------|
| `cs` | C# Class Library 모듈 |
| `ts` | NodeJS 라이브러리 모듈 |
| `upm` | Unity Package 컨테이너 |
| `data` | 데이터 산출 컨테이너 |

### 생성 파일 경로 규칙 (고정)

| Target | 모듈 루트 | 생성 파일 위치 |
|--------|----------|---------------|
| `cs` | `{csTargetDir}` | `{csTargetDir}/generated/**` |
| `ts` | `{tsTargetDir}` | `{tsTargetDir}/generated/**` |
| `upm` | `{upmTargetDir}` | `{upmTargetDir}/Runtime/**` |
| `data` | `{dataTargetDir}` | `{dataTargetDir}/json/**` |

### Table API 생성 구조

Table API 생성물은 `namespace Devian.Table` 하위에 `TB_<TableName>` 정적 타입으로 생성된다.

```csharp
namespace Devian.Tables
{
    public sealed class TB_TestTable : ITableContainer
    {
        public void Clear() { ... }
        public void LoadFromJson(string json, LoadMode mode = LoadMode.Merge) { ... }
        public string SaveToJson() { ... }
        public void LoadFromBase64(string base64, LoadMode mode = LoadMode.Merge) { ... }
        public string SaveToBase64() { ... }
    }
}
```

---

## 4. 빌드 실행 정책

`devian build` 실행 시:

1. `domains`를 순회
2. 각 domain에서:
   - Target Type별 **모듈 스캐폴드 ensure**
     - `csTargetDirs` → C# Class Library 스캐폴드
     - `tsTargetDirs` → NodeJS 라이브러리 스캐폴드
     - `upmTargetDirs` → UPM 패키지 스캐폴드
   - [Data domain] `contractsDir` + `contractFiles` 패턴으로 contracts 생성
   - [Data domain] `tablesDir` + `tableFiles` 패턴으로 tables 생성
   - [Protocol-only domain] `protocolsDir` + `protocolFile`로 protocols 생성 (단일 파일)
3. 생성은 항상 `{tempDir}/{domain}/{cs|ts|data}`
4. 생성 후 **고정 하위 폴더**로 copy:
   - CS: `{csTargetDir}/generated/`
   - TS: `{tsTargetDir}/generated/`
   - UPM: `{upmTargetDir}/Runtime/`
   - DATA: `{dataTargetDir}/json/`
5. (옵션) Common 도메인 빌드 결과 → Devian.Common / devian-common 모듈 동기화

### Devian.Common / devian-common 모듈 동기화

**Common 도메인 빌드 산출물은 언어별 모듈로 복사된다.**

| 언어 | 소스 | 대상 |
|------|------|------|
| C# | Common 도메인 빌드 결과 | `framework/cs/Devian.Common/generated/` |
| TS | Common 도메인 빌드 결과 | `framework/ts/devian-common/generated/` |

### generated / manual 소유권 규칙

| 폴더 | 소유권 | 규칙 |
|------|--------|------|
| `generated/` | 기계 | Common 도메인 빌드 결과만. 사람이 수정 금지. **커밋 필수** |
| `manual/` | 사람 | 개발자 직접 작성. 생성기 **덮어쓰기 금지** |

> **중요:** 도메인(Common)과 모듈(Devian.Common)은 동일하지 않다. 모듈은 도메인의 산출물을 담는 그릇이다.

### 모듈 스캐폴드 규칙

| Target | 스캐폴드 파일 |
|--------|-------------|
| `cs` | `.csproj` |
| `ts` | `package.json`, `tsconfig.json`, `index.ts` |
| `upm` | `package.json`, `Runtime/*.asmdef`, `Editor/*.asmdef` |

- 스캐폴드 파일이 **없으면 생성**, **있으면 절대 덮어쓰지 않음**
- UPM에서 `README.md`, `LICENSE`, `manifest.json`은 **생성하지 않음**

---

## 5. dependsOnCommon 정책

`dependsOnCommon: true` (기본값) 인 domain은:

| 유형 | 병합 |
|------|------|
| contracts | Common + domain |
| tables | Common + domain (domain이 override) |
| protocols | domain만 (Common 병합 안 함) |

---

## 6. Protocol namespace 정책

- namespace = 파일명 (확장자 제외)
- JSON 내부 `namespace`는 검증만 (생성 기준으로 사용 금지)

예: `ws.json` → namespace는 `ws`

---

## 7. v9 스키마 요약

**v9에서 domains와 protocols는 완전히 분리된다:**

### domains (Data Domain)

| 키 | 설명 |
|-----|------|
| `domains[name].contractsDir` | 컨트랙트 입력 디렉터리 |
| `domains[name].contractFiles` | 컨트랙트 파일 패턴 (예: `["*.json"]`) |
| `domains[name].tablesDir` | 테이블 입력 디렉터리 |
| `domains[name].tableFiles` | 테이블 파일 패턴 (예: `["*.xlsx"]`) |

### protocols (IDL Link Domain)

| 키 | 설명 |
|-----|------|
| `protocols[name].protocolsDir` | 프로토콜 입력 디렉터리 |
| `protocols[name].protocolFile` | 프로토콜 파일명 (단수, 예: `"C2Game.proto"`) |

**핵심:** `protocolFile`은 단일 파일명이며 `{protocolsDir}/{protocolFile}`로 resolve한다.

---

## Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | `domains`: tables + contracts만 (Data domain) |
| 2 | `protocols`: protocolsDir + protocolFile만 (IDL link) |
| 3 | 생성은 항상 tempDir → targetDirs copy |
| 4 | Protocol namespace = 파일명 |
| 5 | v9 필수 필드 누락 시 빌드 실패 |
| 6 | domains와 protocols는 섞이지 않음 |

---

## 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | domains 안에 protocolsDir/protocolFile 추가 (v8 혼합) |
| 2 | protocols 안에 tablesDir/contractsDir 추가 |
| 3 | `protocolFiles` (복수형) 사용 → `protocolFile` (단수) 사용 |
| 4 | Protocol JSON 내부 namespace를 생성 기준으로 사용 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `63-build-runner` | 빌드 실행 |
| `70-common-domain-policy` | Common 도메인 정책 |

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

## 한 줄 요약

**v9: `domains`(contracts/tables)와 `protocols`(protocolsDir/protocolFile)가 분리되어 빌드.**
