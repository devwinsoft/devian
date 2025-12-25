# Devian – 26 Domain Scaffold Generator

## Purpose

**Domain Scaffold Generator creates minimal, language-first contract skeletons,  
and does not generate server or application structure.**

> Scaffold는 "편의 도구"이지, "아키텍처 강제 장치"가 아니다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| contracts 폴더 구조 생성 | `contracts/csharp/`, `contracts/ts/` |
| 언어별 도메인 폴더 생성 | `contracts/{language}/{domain}/` |
| 도메인 단위 최소 스캐폴드 | `domain.config.json`, `src/`, `generated/` |

### Out of Scope

| 항목 | 설명 |
|------|------|
| NestJS 모듈 | ❌ 생성하지 않음 |
| 서버 디렉토리 | ❌ 생성하지 않음 |
| DI / Controller / Service | ❌ 생성하지 않음 |
| 런타임 초기화 코드 | ❌ 생성하지 않음 |
| generated 코드 | ❌ tablegen/codegen의 역할 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | `contracts/{language}/` 폴더가 없으면 생성한다 (최소: `csharp/`, `ts/`) |
| 2 | 도메인 생성 시 **모든 언어 폴더 아래에** 동일한 도메인 이름을 생성한다 |
| 3 | 각 도메인에는 최소한 `domain.config.json`, `src/`, `generated/`가 존재해야 한다 |

### 도메인 생성 시 경로 (Hard)

```
contracts/csharp/{domain}/
contracts/ts/{domain}/
```

### 최소 파일 구조 (Hard)

```
contracts/{language}/{domain}/
├── domain.config.json
├── src/           (비어 있어도 됨)
└── generated/     (비어 있어도 됨)
```

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | `common/` 도메인은 옵션으로 생성 가능 |
| 2 | scaffold는 비어 있는 구조를 선호한다 |
| 3 | 파일 수를 늘리지 않는다 |

---

## Explicit WON'T (강조)

| 항목 | 설명 |
|------|------|
| NestJS 서버 구조 생성 | ❌ 절대 생성하지 않음 |
| 특정 서버 전제 | ❌ 어떤 서버도 전제하지 않음 |
| generated 코드 생성 | ❌ tablegen/codegen의 역할 |
| 런타임 코드 생성 | ❌ Consumer 책임 |

---

## Inputs

### Required

| Parameter | 설명 | 예시 |
|-----------|------|------|
| `domainName` | 도메인 이름 | common, auth, ingame |

### Optional Flags

| Flag | Default | 설명 |
|------|---------|------|
| `--langs=csharp,ts` | `csharp,ts` | 생성할 언어 |

---

## Outputs

### C# Domain Output

```
contracts/csharp/{domain}/
├── domain.config.json
├── src/
├── generated/
└── {Domain}.csproj
```

### TypeScript Domain Output

```
contracts/ts/{domain}/
├── domain.config.json
├── src/
│   └── index.ts
├── generated/
└── package.json
```

---

## Special Case: common domain

- `common`은 언어별로 동일하게 생성한다:
  - `contracts/csharp/common`
  - `contracts/ts/common`
- `common`은 도메인들 간 공용 타입의 기준점이다

---

## Responsibilities

1. **"언어 우선 contracts 구조"를 물리적으로 보장**
2. **이후 모든 codegen 스킬의 경로 기준점(anchor) 역할**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | scaffold 결과만 보고도 contracts 구조가 즉시 이해된다 |
| 2 | 서버/NestJS 관련 파일이 **단 하나도** 생성되지 않는다 |
| 3 | 다른 스킬 문서에서 scaffold 결과 경로를 가정해도 모순이 없다 |
| 4 | 모든 언어 폴더에 동일한 도메인이 생성된다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `00-rules-minimal` | 최소 규칙 정의 |
| `01-devian-core-philosophy` | 철학 기준 |
| `20-codegen-protocol` | Protocol codegen |
| `21-codegen-table` | Table codegen |
| `51-generated-integration` | Generated 통합 |
| `90-language-first-contracts` | 언어 우선 구조 정의 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.5.0 | 2024-12-21 | v5: Hard/Soft/WON'T 구분, NestJS 비생성 강조 |
| 0.4.0 | 2024-12-21 | v4: Language-first contracts |
| 0.3.0 | 2024-12-21 | v3: No bundles |
| 0.2.0 | 2024-12-21 | v2: Common-aware |
| 0.1.0 | 2024-12-21 | Initial skill definition |
