# Devian – 70 Common Domain Default Policy

## Purpose

**Devian 프로젝트는 항상 `Common` 도메인을 기본으로 포함한다.**

---

## Belongs To

**Core / Build**

---

## 1. Common 도메인 필수 규칙

- `Common` 도메인은 Devian 프로젝트에 **항상 존재해야 한다**
- `Common` 도메인은:
  - 공통 contracts
  - 공통 table seed / 기본 데이터
  - 공통 타입 및 검증 기준
  을 제공한다
- CI 및 로컬 기본 테스트는 **항상 `Common` 도메인 빌드 기준**으로 수행한다

---

## 2. 입력 디렉토리 구조 (v9)

**build.json v9에서 domains와 protocols는 목적이 다르며 섞이지 않는다.**

```
input/
├── Common/                ← domains 섹션 (Data)
│   ├── contracts/         ← 공통 타입
│   │   └── TestContract.json
│   └── tables/            ← 공통 테이블
│       └── TestTable.xlsx
├── C2Game/                ← protocols 섹션 (IDL)
│   └── protocols/
│       └── C2Game.proto
├── Game2C/                ← protocols 섹션 (IDL)
│   └── protocols/
│       └── Game2C.proto
└── build/
    └── build.json
```

**v9 분리 규칙:**
- **domains (Data)**: `contractsDir`/`contractFiles` + `tablesDir`/`tableFiles` 사용
- **protocols (IDL)**: `protocolsDir`/`protocolFile` 사용 (단일 파일)

---

## 3. dependsOnCommon 규칙

- 모든 domain은 기본적으로 `dependsOnCommon = true`
- domain 설정에 명시되지 않으면 **true**
- `Common` 도메인 자신에게는 이 옵션을 적용하지 않는다

---

## 4. dependsOnCommon = true 의 의미

`dependsOnCommon = true` 인 domain `X`는 빌드 시:

| 유형 | 입력 집합 |
|------|----------|
| Contracts | input/Common/contracts + input/X/contracts |
| Tables | input/Common/tables + input/X/tables |

> protocols는 domains에 속하지 않으므로 별도 처리

---

## 5. 충돌 처리 규칙

| 유형 | 규칙 |
|------|------|
| contracts 충돌 | 동일 파일/타입 충돌 시 **빌드 실패** |
| tables 충돌 | domain(X)이 Common을 override |

---

## 6. Domain Root (정본)

**Devian에서 Domain은 디렉터리 이름이 아니라 논리 단위이다.**

모든 Domain의 입력 경로는 build.json의 도메인별 설정에서 명시된다.

### v9 스키마 (정본)

**domains (Data - Common 예시):**
```
domains[name].contractsDir  → contracts 디렉터리
domains[name].contractFiles → contracts 파일 패턴 (예: ["*.json"])
domains[name].tablesDir     → tables 디렉터리
domains[name].tableFiles    → tables 파일 패턴 (예: ["*.xlsx"])
```

**protocols (IDL - C2Game 예시):**
```
protocols[name].protocolsDir  → protocols 디렉터리
protocols[name].protocolFile  → protocol 파일명 (단수, 예: "C2Game.proto")
```

`domains/` 디렉터리는 Devian 구조에 존재하지 않는다.
`{Domain}` 표기는 문서/설명용 플레이스홀더이며,
실제 파일 시스템 경로를 의미하지 않는다.

---

## 7. Protocol 규약 (v9)

**수동 작성 proto 폴더(proto-manual)는 폐기되었다.**

IDL(.proto)은 `input/{Domain}/protocols/{Domain}.proto` 단일 파일만 사용한다.

### Protocol-only Domain 구조

```
input/C2Game/
└── protocols/
    └── C2Game.proto      # 단일 파일

input/Game2C/
└── protocols/
    └── Game2C.proto      # 단일 파일
```

### cross-domain 참조 규칙

| 참조 | 허용 | 비고 |
|------|:----:|------|
| `ref:Common.{Name}` | ✅ | 다른 도메인이 Common 참조 |
| `ref:OtherDomain.{Name}` | ❌ | 1단계에서 금지 |
| Common → 다른 도메인 | ❌ | 의존 역전 금지 |

---

## 8. Devian.Common / devian-common 모듈 (정본)

**Common 도메인 빌드 산출물 + 개발자 manual 코드를 결합한 모듈**

### 모듈 구조

```
framework/cs/Devian.Common/
├── generated/   # Common 도메인 빌드 결과 (기계 생성, 커밋)
└── manual/      # 개발자 작성 코드 (생성기 덮어쓰기 금지)

framework/ts/devian-common/
├── generated/   # Common 도메인 빌드 결과 (기계 생성, 커밋)
└── manual/      # 개발자 작성 코드 (생성기 덮어쓰기 금지)
```

### 도메인 vs 모듈 (중요)

| 구분 | 설명 |
|------|------|
| **Common 도메인** | 스키마/데이터/타입의 출처 (Data domain) |
| **Devian.Common 모듈** | Common 도메인 빌드 산출물 + manual 코드 결합. 런타임 의존 대상 |

> **도메인(Common)**과 **모듈(Devian.Common)**은 동일하지 않다. 모듈은 도메인의 산출물을 담는 그릇이다.

---

## 9. Hard Rules (MUST)

| # | 규칙 |
|---|------|
| 1 | Common 도메인은 항상 존재해야 한다 |
| 2 | 모든 domain은 기본적으로 dependsOnCommon = true |
| 3 | CI/테스트는 Common 도메인 빌드 기준 |
| 4 | contracts 충돌 시 빌드 실패 |
| 5 | tables 충돌 시 domain이 Common을 override |
| 6 | **Devian.Common의 generated는 기계 생성, manual은 사람 소유** |

---

## 10. 절대 금지 사항 (MUST NOT)

| # | 금지 |
|---|------|
| 1 | Common 없이 domain을 설계하거나 생성 |
| 2 | Common을 선택 사항(optional)으로 취급 |
| 3 | Common을 runtime 의존성 집합으로 사용 |
| 4 | Common이 다른 domain의 산출물을 import |
| 5 | **생성기가 manual 폴더 덮어쓰기** |
| 6 | **별도 도메인 루트 디렉터리 생성/사용** |

---

## 11. build.json 예시 (v9)

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
- Common은 **domains** (tables + contracts만)
- C2Game/Game2C는 **protocols** (protocolsDir + protocolFile)
- **protocolFile은 단일 파일명이고 `{protocolsDir}/{protocolFile}`로 resolve한다**

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | 빌드 스펙 |
| `63-build-runner` | 빌드 실행 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 3.0.0 | 2025-12-28 | v9 스키마: domains/protocols 분리 |
| 2.0.0 | 2025-12-28 | v8 스키마: 섹션 배타 규칙, protocolFile 단수 |
| 1.3.0 | 2025-12-28 | Phase 3 완료: xlsx/proto(JSON) 지원 |
| 1.2.0 | 2025-12-28 | Phase 2 완료 (v7 스키마 지원) |
| 1.1.0 | 2025-12-28 | v7 스키마 (Dir + Files 패턴) |
| 1.0.0 | 2025-12-28 | Initial |

## 한 줄 요약

**v9: domains(Common - tables+contracts)와 protocols(C2Game/Game2C - IDL)가 분리됨. Common은 개발과 테스트의 기준점.**
