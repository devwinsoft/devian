# Devian v10 — SSOT (Root)

Status: ACTIVE
AppliesTo: v10
SSOT: this file

## Purpose

이 문서는 **Devian v10의 공통 정책(Policy)**을 정의하는 **Root SSOT**이다.

- **공통 용어 / 플레이스홀더 / 입력 분리 / 머지 규칙**을 정의한다.
- 카테고리별 상세 정책은 **Category SSOT**로 위임한다.

---

## Category SSOT

상세 정책은 카테고리별 SSOT를 참조한다:

| Category | SSOT | 범위 |
|----------|------|------|
| **Tools** | [devian-tools/03-ssot](../../../devian-tools/03-ssot/SKILL.md) | 빌드 파이프라인, Phase, Validate, tempDir, Clean+Copy, TS Workspace |
| **Builder** | [devian-tools/11-builder/03-ssot](../../../devian-tools/11-builder/03-ssot/SKILL.md) | tableConfig, Tables, NDJSON, pb64, DATA 산출물, Protocol Spec, Opcode/Tag, Protocol UPM |
| **Unity** | [devian-unity/03-ssot](../../../devian-unity/03-ssot/SKILL.md) | upmConfig, UPM Sync, Foundation, samplePackages, Unity Gate |

---

## SSOT 우선순위

1. **`skills/devian/10-module/03-ssot/SKILL.md`** (이 문서) — 공통 정책 정본
2. **Category SSOT** — 카테고리별 정책 정본
3. **`{buildInputJson}`** (예: `input/input_common.json`) — 실제 빌드 스키마/경로 정본
4. **런타임/제너레이터 코드** — 실제 동작 정본

SSOT 간 충돌이 발생하면:

- **정책(이 문서) ↔ `{buildInputJson}`** 충돌은 **`{buildInputJson}`이 우선**
- **정책 ↔ 코드** 충돌은 **코드가 우선**
- 정책을 유지하고 싶다면 **코드를 바꾸는 결정**이 필요하다.

---

## "충돌"의 의미 (필수)

Devian 문서/대화에서 말하는 "충돌"은 기능 자체의 찬반/의견 충돌이 아니다.

- **SSOT 불일치(Hard)**: 문서(SKILL/`{buildInputJson}`)에 적힌 규약/경로/정책이 실제 코드/설정/산출물과 **다른 상태**
  - 예: 문서에는 `excludePlatforms: ["WebGL"]`인데 실제 asmdef는 `excludePlatforms: []`
  - 예: 문서에는 `WsNetClient`인데 실제 코드는 `WebSocketClient`
- 결론: "WebGL 지원" 같은 기능은 가능/불가능이 아니라, **문서와 구현의 일치 여부**만 문제다.

---

## 용어 (필수)

문서/대화에서 아래 용어를 강제한다. **"domain" 단독 사용 금지**.

| 용어 | 의미 | 예시 |
|---|---|---|
| **DomainType** | 종류 | `DATA`, `PROTOCOL` |
| **DomainKey** | DATA 도메인의 `{buildInputJson}` 키 | `Common` |
| **ProtocolGroup** | PROTOCOL 그룹명 (`{buildInputJson}` `group` 필드) | `Client` |
| **ProtocolName** | PROTOCOL 파일명 base | `C2Game`, `Game2C` |

---

## 플레이스홀더 표준 (필수)

문서/대화에서 `{domain}`, `{name}` 같은 범용 플레이스홀더를 금지한다.

허용 플레이스홀더:

- `{tempDir}` — `{buildInputJson}`의 `tempDir` 값
- `{DomainKey}`
- `{ProtocolGroup}`
- `{ProtocolName}`
- `{csConfig.generateDir}`, `{tsConfig.generateDir}` — 전역 C#/TS 반영 루트
- `{tableConfig.tableDirs}` — 테이블 출력 타겟 (배열)
- `{tableConfig.stringDirs}` — String 테이블 출력 타겟 (배열)
- `{tableConfig.soundDirs}` — Sound 데이터 출력 타겟 (배열)

> `{tempDir}`는 절대 경로가 아닌 경우 **`{buildInputJson}`이 있는 디렉토리** 기준으로 해석한다.

---

## Input 포맷 분리 (Hard Rule)

**빌드 설정은 config.json과 input.json으로 분리한다.**

| 파일 | 역할 | 허용 키 |
|------|------|---------|
| `input/config.json` | 공통 설정 (경로/타겟) | csConfig, tsConfig, tableConfig, upmConfig, samplePackages |
| `input/input_*.json` | 빌드 스펙 (도메인/프로토콜) | version, configPath, tempDir, domains, protocols |

**금지 키 (Hard FAIL):**
- config.json에 `tempDir`, `domains`, `protocols` 존재 → FAIL
- config.json에 `staticUpmPackages` 존재 → FAIL (forbidden, `samplePackages` 사용)
- input.json에 `csConfig`, `tsConfig`, `tableConfig`, `upmConfig`, `samplePackages` 존재 → FAIL
- config.json에 `dataConfig` 존재 → FAIL (deprecated, `tableConfig` 사용)

**Deprecated 금지 (Hard FAIL):**
- framework/upm 내에서 deprecated/fallback 레이어를 추가하거나 유지하는 것을 금지한다.
- 구조/설정 체계가 바뀌면, 동일 작업에서 기존 레거시 코드를 즉시 삭제하고 사용처도 함께 정리해야 한다.

**상대경로 기준 (중요):**
- 모든 상대경로 해석 기준은 **input json 파일이 있는 폴더 (buildJsonDir, 보통 `input/`)**
- config.json 자신의 디렉토리를 기준으로 해석하면 **FAIL**

**머지 규칙:**
```
finalConfig = deepMerge(config.json, input.json)
```
- tempDir은 input.json 값이 최종값 (input 우선)
- 그 외 키가 양쪽에 있으면 **FAIL**

---

## C# 런타임 모듈 구조 (Hard Rule)

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

**생성물 namespace 규칙 (Hard Rule):**
- 프로토콜 생성물은 `Devian.Protocol.{ProtocolGroup}`을 사용한다.
- Domain 생성물은 `Devian.Domain.{DomainKey}`를 사용한다.

---

## TS 런타임 모듈 구조 (Hard Rule)

**Devian TS 런타임은 단일 패키지(@devian/core)로 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| 단일 패키지 | `framework-ts/module/devian` | Core + Network + Protobuf 통합 |

**생성물 패키지명 유지 (Hard Rule):**
- 프로토콜 생성물은 `@devian/protocol-{protocolgroup}` 이름을 유지한다.
- 모듈 생성물은 `@devian/module-{domainkey}` 이름을 유지한다.

---

## 모듈 의존성 (Hard Rule)

**C# DATA Domain 모듈 의존성:**
- `Devian.Domain.{DomainKey}.csproj`는 `..\Devian\Devian.csproj`만 ProjectReference 한다.

**C# PROTOCOL 모듈 의존성:**
- `Devian.Protocol.{ProtocolGroup}.csproj`는 다음을 ProjectReference 한다:
  - `..\Devian\Devian.csproj`
  - `..\Devian.Domain.Common\Devian.Domain.Common.csproj`

**TS DATA Domain 패키지 의존성:**
- `@devian/module-{domainkey}`는 `@devian/core`만 의존한다.

**TS PROTOCOL 패키지 의존성:**
- `@devian/protocol-{protocolgroup}`는 `@devian/core` + `@devian/module-common`을 의존한다.

---

## Hard Conflicts (DoD)

아래는 발견 즉시 FAIL(반드시 0개)로 취급한다.

1. 입력 포맷이 서로 다르게 서술됨 (예: PROTOCOL이 .proto/IDL이라고 서술)
2. opcode/tag 규칙이 비결정적으로 서술됨 (재배정/랜덤/비결정 허용)
3. `{buildInputJson}`과 경로/플레이스홀더 규약이 불일치
4. Reserved 옵션(`parser:*` 등)을 강제/필수/의미로 서술
5. 코드와 다른 API/산출물/프레임 규약을 SKILL이 "정본"처럼 단정
6. TS `index.ts`를 통째로 덮어쓰는 동작 (marker 갱신 방식 위반)
7. `domains.Common`이 `{buildInputJson}`에 없는 상태

---

## Soft Conflicts (충돌 아님)

- 용어/표기/톤 차이
- 문서 링크가 끊김

단, Soft가 Hard 오해를 유발하면 개선 대상이다.

---

## Examples (예제 도메인)

**DomainKey = `Game`** 을 기준으로 한 예제 입력과 흐름 안내:

> **진입점:** [skills/devian-examples/01-policy](../../../devian-examples/01-policy/SKILL.md)

예제 입력 위치:
- `devian/input/Domains/Game/*.json` — 컨트랙트 예제
- `devian/input/Domains/Game/*.xlsx` — 테이블 예제
- `devian/input/Protocols/Game/**` — 프로토콜 예제

---

## Reference

- **공통 정책 정본:** 이 문서 (`skills/devian/10-module/03-ssot/SKILL.md`)
- **카테고리 SSOT:**
  - [Tools SSOT](../../../devian-tools/03-ssot/SKILL.md)
  - [Data SSOT](../../../devian-tools/11-builder/03-ssot/SKILL.md)
  - [Protocol SSOT](../../../devian-tools/11-builder/03-ssot/SKILL.md)
  - [Unity SSOT](../../../devian-unity/03-ssot/SKILL.md)
- **빌드 스키마 정본:** `{buildInputJson}` (예: `input/input_common.json`)
- **동작 정본:** 런타임/제너레이터 코드
