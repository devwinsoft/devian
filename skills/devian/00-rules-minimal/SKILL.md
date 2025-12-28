# Devian – 00 Rules Minimal

## Purpose

**Devian Framework의 핵심 규칙을 정의한다.**

이 문서는 Devian 전체에서 **반드시 지켜야 하는 Hard Rules**만 정의한다.

---

## Scope

### In Scope (Framework 책임)

| 항목 | 설명 |
|------|------|
| 정의 포맷 | contracts, tables, protocols JSON 스키마 |
| 빌드 규약 | `input/` 중심, `build.json` 단일 진실 |
| 코드/데이터 생성 규칙 | codegen 출력 형태, 네임스페이스 규칙 |

### Out of Scope (Skill 책임)

| 항목 | 담당 Skill |
|------|-----------|
| Unity AssetBundle / Addressables | Unity Skill |
| NestJS 서버 아키텍처 | NestJS Server Skill |
| 런타임 로더/프로바이더 구현 | 각 플랫폼 Skill |

---

## Hard Rules (MUST)

### Hard Rule 1: Source of truth는 `input/`이며, 빌드는 `build.json`이 지휘한다

| # | Rule |
|---|------|
| 1-1 | 버전 관리는 **`input/` 파일만**을 기준으로 한다 |
| 1-2 | 빌드 스크립트는 `input/build/build.json`만 읽고, 도메인/타겟/출력 경로를 결정한다 |
| 1-3 | `contracts/` 같은 "중간 정규화 저장소"는 **필수가 아니다** (선택/레거시) |

### Hard Rule 2: Temp workspace는 삭제 가능해야 한다

| # | Rule |
|---|------|
| 2-1 | 빌드는 임시 작업장을 사용한다 (예: `.devian/work/`) |
| 2-2 | 임시 작업장은 **언제든 삭제 가능**해야 하며, Git에서 제외 권장 |
| 2-3 | 동일 input → 동일 output (결정적 빌드) |

### Hard Rule 3: Output 경로/폴더명은 Devian이 강제하지 않는다

| # | Rule |
|---|------|
| 3-1 | Devian은 `generated/` 같은 폴더명을 **강제하지 않는다** |
| 3-2 | Unity asmdef 제약(예: `Runtime/`)은 팀/엔진 규칙이며, Devian이 소유하지 않는다 |
| 3-3 | **출력 경로는 build.json이 단일 진실**이며, 스크립트는 이를 그대로 따른다 |

### Hard Rule 4: 런타임 구현은 Skill이다

| # | Rule |
|---|------|
| 4-1 | Framework는 NestJS 서버 구조를 **생성하지 않는다** (NestJS Skill 책임) |
| 4-2 | Framework는 Unity 엔진 로직을 **포함하지 않는다** (Unity Skill 책임) |
| 4-3 | **모든 런타임 연결은 Skill**이다 |

### Hard Rule 5: 모듈 의존성 정책

```
contracts (standalone)
    ↑
tables (depends on contracts)
    ↑
IDL (depends on contracts + tables)
```

| # | Rule |
|---|------|
| 5-1 | **contracts는 독립적**이다 — tables, IDL을 import/require하지 않는다 |
| 5-2 | **tables는 proto 정의에 의존**할 수 있다 — ref:{Name} 참조 |
| 5-3 | **IDL은 contracts와 tables에 의존**할 수 있다 |
| 5-4 | **역방향 의존 금지** — contracts ← tables ← IDL 순서 위반 불가 |

### Hard Rule 6: Table Primary Key 규칙

| # | Rule |
|---|------|
| 6-1 | **Primary Key는 `key:true` 옵션으로 지정** (Row 3 Options) |
| 6-2 | **`key:true`는 최대 1개** (복합키 미지원) |
| 6-3 | `key:true` 없으면 Entity만 생성, 컨테이너/로더 미생성 |
| 6-4 | **Key 타입: `ref:*` 금지** |
| 6-5 | **Key 타입: 배열 타입 금지** |
| 6-6 | 허용되는 Key 타입: `byte`, `ubyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `string` |

### Hard Rule 7: Table Header 구조 (4줄 고정)

| # | Rule |
|---|------|
| 7-1 | **Row 1: Field Name** (필드 이름) |
| 7-2 | **Row 2: Type** (Scalar 10종, ref:{Name}, 배열) |
| 7-3 | **Row 3: Options** (`key:true/false`, `parser:json`, `optional:true/false`) |
| 7-4 | **Row 4: Comment** (순수 설명용 주석 — **Devian은 절대 해석하지 않음**) |
| 7-5 | **Row 5+: Data** (실제 데이터) |
| 7-6 | **Row 4에 meta/option/policy/constraint 개념은 없다** |
| 7-7 | **tablegen, validator, runtime 어디에서도 Row 4를 로직에 사용하지 않는다** |
| 7-8 | **byte/ubyte/short/ushort 범위 검증은 Generator/Loader 책임** |
| 7-9 | **Converter는 parse 성공/실패만 책임** |


### Hard Rule 8: Skill 확장 정책

| # | Rule |
|---|------|
| 8-1 | **모든 확장은 Skill**로 정의한다 |
| 8-2 | Skill은 Framework 규약을 **변경하지 않는다** |
| 8-3 | Skill은 정의 포맷을 **독점하지 않는다** |
| 8-4 | Skill은 단일 빌드 흐름에 **종속된다** |

### Hard Rule 9: 도메인 단일 네임스페이스

| # | Rule |
|---|------|
| 9-1 | 도메인당 **하나의 네임스페이스**만 사용한다 |
| 9-2 | 하위 네임스페이스(`Devian.Common.*`)는 **금지** |
| 9-3 | `common` → `Devian.Common`, `ws` → `Devian.Ws` |

### Hard Rule 10: 소유권 및 금지 사항

| # | Rule |
|---|------|
| 10-1 | 사람이 `generated/**` 수정 **금지** |
| 10-2 | 생성기가 `manual/**` 덮어쓰기 **금지** |
| 10-3 | Common 외 cross-domain ref **금지** (1단계) |
| 10-4 | ref 대상 정의가 **0개 또는 2개 이상**이면 **생성 실패** |

> **Note:** 수동 작성 proto 폴더(proto-manual)는 폐기되었다.

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | input 스펙은 가능한 한 **중립 포맷(JSON)**을 사용한다 |
| 2 | `build.json`은 MVP 팀(1–3인)이 수정하기 쉬운 형태로 유지한다 |
| 3 | 빌드 결과를 추적하고 싶다면 `build.lock.json`(옵션)을 남긴다 |

> C# Roslyn 파싱 기반의 `.cs` 입력은 크로스플랫폼 관점에서 비권장(레거시 허용 가능).

---

## Responsibilities

1. **Devian 전반의 "최소 강제" 원칙 유지**
2. **build.json 중심 설계가 다른 스킬 문서와 충돌하지 않도록 기준 제공**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Devian 문서 어디에서도 "framework를 강제한다"는 인상이 없다 |
| 2 | `input/`이 source of truth임이 문서적으로 명확하다 |
| 3 | output 폴더 규칙이 Devian에 의해 강제되지 않는다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `01-devian-core-philosophy` | Framework 철학 |
| `02-skill-specification` | Skill 공식 스펙 |
| `60-build-pipeline` | Build 규약 |
| `21-codegen-table` | Table codegen |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
