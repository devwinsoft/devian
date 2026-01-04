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
2. **`build.json`** — 실제 빌드 스키마/경로 정본
3. **런타임/제너레이터 코드** — 실제 동작 정본

SSOT 간 충돌이 발생하면:

- **정책(이 문서) ↔ build.json** 충돌은 **build.json이 우선**
- **정책 ↔ 코드** 충돌은 **코드가 우선**
- 정책을 유지하고 싶다면(코드가 아니라 정책이 정답이어야 한다면) **코드를 바꾸는 결정**이 필요하다.

---

## 용어 (필수)

문서/대화에서 아래 용어를 강제한다. **"domain" 단독 사용 금지**.

| 용어 | 의미 | 예시 |
|---|---|---|
| **DomainType** | 종류 | `DATA`, `PROTOCOL` |
| **DomainKey** | DATA 도메인의 build.json 키 | `Common` |
| **ProtocolGroup** | PROTOCOL 그룹명 (build.json `group` 필드) | `Client` |
| **ProtocolName** | PROTOCOL 파일명 base | `C2Game`, `Game2C` |

---

## 플레이스홀더 표준 (필수)

문서/대화에서 `{domain}`, `{name}` 같은 범용 플레이스홀더를 금지한다.

허용 플레이스홀더:

- `{tempDir}` — build.json의 `tempDir` 값
- `{DomainKey}`
- `{ProtocolGroup}`
- `{ProtocolName}`
- `{csTargetDir}`, `{tsTargetDir}`, `{dataTargetDir}`, `{upmTargetDir}`

> `{tempDir}`는 절대 경로가 아닌 경우 **build.json이 있는 디렉토리** 기준으로 해석한다.

---

## 빌드 파이프라인 정책

### Phase 모델

빌드는 **3단계**로 해석한다.

1. **Collect**: 입력 파일 수집 (build.json 기반)
2. **Generate**: 모든 산출물은 **staging({tempDir})에만 생성**
3. **Copy**: staging → targetDirs로 **clean + copy**

> staging({tempDir}) 외의 위치에 직접 생성하는 동작은 금지한다.

### Clean + Copy 정책

- Copy 단계는 targetDir을 **clean 후 copy**한다.
- **충돌 방지 책임은 build.json 설계(타겟 분리)에 있다.**
  - 동일 targetDir에 여러 DomainKey/ProtocolGroup을 매핑하면, clean 단계 때문에 서로 덮어쓰거나 삭제될 수 있다.

---

## 입력 규약

### 경로 해석 규칙

build.json 위치는 유동적이다. 현재 프로젝트에서는 `input/build.json`에 위치한다.

**build.json 내의 모든 상대 경로는 build.json이 위치한 디렉토리 기준으로 해석된다.**

예시 (build.json이 `input/build.json`에 있을 때):
- `contractsDir: "Common/contracts"` → `input/Common/contracts`
- `csTargetDir: "../framework/cs"` → `framework/cs`
- `tempDir: "temp"` → `input/temp`

### 1) DomainType = DATA

DATA 입력은 build.json의 `domains` 섹션이 정의한다.

필수 개념:

- **Contracts**: JSON 기반 타입/enum 정의
- **Tables**: XLSX 기반 테이블 정의 + 데이터

입력 경로는 build.json이 정본이다. 예:

- `domains[Common].contractsDir = Common/contracts`
- `domains[Common].tablesDir = Common/tables`

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

- `key:true`만 PrimaryKey로 해석
- `optional:true`는 "nullable/optional column" 힌트로만 사용
- 그 외 `parser:*` 등은 **Reserved** (있어도 무시 / 의미 부여 금지)

> 상세 타입 지원/파서 동작/산출 코드 형태는 런타임/제너레이터 코드를 정답으로 본다.

#### DATA 산출물 경로(정책)

- staging:
  - `{tempDir}/{DomainKey}/cs/generated/{DomainKey}.g.cs`
  - `{tempDir}/{DomainKey}/ts/generated/{DomainKey}.g.ts`, `{tempDir}/{DomainKey}/ts/index.ts`
  - `{tempDir}/{DomainKey}/data/json/**.ndjson`
- final:
  - `{csTargetDir}/Devian.Module.{DomainKey}/generated/{DomainKey}.g.cs`
  - `{tsTargetDir}/devian-module-{domainkey}/generated/{DomainKey}.g.ts`, `index.ts`
  - `{dataTargetDir}/{DomainKey}/json/**`

> Domain의 모든 Contract, Table Entity, Table Container는 단일 파일(`{DomainKey}.g.cs`, `{DomainKey}.g.ts`)에 통합 생성된다.

### 2) DomainType = PROTOCOL

PROTOCOL 입력은 build.json의 `protocols` 섹션(배열)이 정의한다.

```json
"protocols": [
  {
    "group": "Client",
    "protocolDir": "./Protocols/Client",
    "protocolFiles": ["C2Game.json", "Game2C.json"],
    "csTargetDir": "../framework/cs",
    "tsTargetDir": "../framework/ts"
  }
]
```

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
- staging: `{tempDir}/Devian.Network.{ProtocolGroup}/{ProtocolName}.g.cs`
- final: `{csTargetDir}/Devian.Network.{ProtocolGroup}/{ProtocolName}.g.cs`
- 프로젝트 파일: `{csTargetDir}/Devian.Network.{ProtocolGroup}/Devian.Network.{ProtocolGroup}.csproj`

**TypeScript:**
- staging: `{tempDir}/{ProtocolGroup}/{ProtocolName}.g.ts`, `index.ts`
- final: `{tsTargetDir}/devian-network-{protocolgroup}/{ProtocolName}.g.ts`, `index.ts`

---

## Hard Conflicts (DoD)

아래는 발견 즉시 FAIL(반드시 0개)로 취급한다.

1. 입력 포맷이 서로 다르게 서술됨 (예: PROTOCOL이 .proto/IDL이라고 서술)
2. opcode/tag 규칙이 비결정적으로 서술됨 (재배정/랜덤/비결정 허용)
3. build.json과 경로/플레이스홀더 규약이 불일치
4. Reserved 옵션(`parser:*` 등)을 강제/필수/의미로 서술
5. 코드와 다른 API/산출물/프레임 규약을 SKILL이 "정본"처럼 단정

---

## Soft Conflicts (충돌 아님)

- 용어/표기/톤 차이
- 문서 링크가 끊김

단, Soft가 Hard 오해를 유발하면 개선 대상이다.

---

## Reference

- **정책 정본:** 이 문서 (`skills/devian/03-ssot/SKILL.md`)
- **빌드 스키마 정본:** `build.json`
- **동작 정본:** 런타임/제너레이터 코드
