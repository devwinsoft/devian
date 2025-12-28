# Devian – 13 Devian Core TypeScript Generator

## Purpose

**Devian 입력 정의(Contracts / Protocols)를 기반으로 TypeScript Core 모듈의 generated 코드를 생성/갱신한다.**

이 스킬은 Devian Framework의 TS 언어 모델(Core)을 제공한다.

---

## Belongs To

**Framework / Core**

> 이 스킬은 Framework Core 라이브러리의 TypeScript 구현을 생성한다.
> C# Core와 **대칭**을 유지한다.

---

## 핵심 원칙

| 원칙 | 설명 |
|------|------|
| `modules/` 비수정 | `modules/`는 생성 산출물 정착지이며, 이 스킬은 수정하지 않음 |
| 정착지 | TS Core의 정착지는 `framework/ts/devian-core` |
| Deterministic | 같은 입력이면 같은 출력 |

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 패키지 골격 생성 | `package.json`, `tsconfig.json`, `src/index.ts` |
| generated 파일 복사 | `modules/ts/**/*.g.ts` → `framework/ts/devian-core/src/generated/` |
| export barrel 생성 | `src/generated/index.g.ts` |

### Out of Scope (다른 Skill 영역)

| 항목 | 담당 |
|------|------|
| `modules/` 생성 | 기존 C# 빌드 파이프라인 |
| Tables (Excel 파싱) | TODO (1차 범위 외) |
| NestJS 서버 코드 | NestJS Server Skill |

---

## Input (Source)

본 저장소에는 이미 TS 생성 파이프라인이 존재한다.

- `framework/cs/Devian.Tools build` 실행 결과로 생성되는 파일을 소스로 사용
- (기본) 입력은 `modules/ts/**/generated/*.g.ts`

현재 도메인 구조:

```
modules/ts/
├── Common/generated/*.g.ts
└── ws/generated/*.g.ts
```

> Tables는 Excel 파싱이 TODO이므로, 1차 범위는 Contracts/Protocols 중심이다.

---

## Output (Target)

### 생성 루트

```
framework/ts/devian-core/src/generated/
```

### 생성 구조

```
framework/ts/devian-core/
├── package.json
├── tsconfig.json
└── src/
    ├── index.ts                    ← export * from "./generated/index.g"
    └── generated/
        ├── index.g.ts              ← export barrel
        ├── common/
        │   ├── UserType.g.ts
        │   ├── UserProfile.g.ts
        │   ├── TestTableRow.g.ts
        │   ├── Table.g.ts
        │   ├── tables.loader.g.ts  ← 테이블 캐시 로더 (신규)
        │   └── ...
        └── ws/
            └── ws.g.ts
```

### Entry Point 규칙

`src/index.ts`는 항상 `src/generated/index.g.ts`를 re-export 한다:

```typescript
export * from "./generated/index.g";
```

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | `modules/**` 아래 파일/폴더를 **수정하지 않는다** |
| 2 | `input/build/build.json`을 **수정하지 않는다** |
| 3 | 기존 C# 생성 파이프라인을 **변경하지 않는다** |
| 4 | 파일 리스트는 **알파벳 정렬** |
| 5 | 줄바꿈은 **LF로 통일** |
| 6 | 생성물에 **timestamp 등 비결정 정보 삽입 금지** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | `--build` 옵션으로 사전 빌드 지원 |
| 2 | `tsc` 컴파일 성공 보장 |

---

## Execution

### 수행 내용

1. **패키지 골격 보장** (없으면 생성)
   - `package.json`, `tsconfig.json`, `src/index.ts`, `src/generated/`

2. **입력 산출물 검증**
   - `modules/ts/Common/generated/*.g.ts`
   - `modules/ts/ws/generated/*.g.ts`
   - **파일 수가 0이면 에러 (exit code 1)**

3. **파일 복제** (Deterministic)
   - `modules/ts/Common/generated/*.g.ts` → `framework/ts/devian-core/src/generated/common/`
   - `modules/ts/ws/generated/*.g.ts` → `framework/ts/devian-core/src/generated/ws/`

4. **Export barrel 생성**
   - `src/generated/index.g.ts` (알파벳 정렬)

### CLI 실행

```bash
node skills/devian/13-devian-core-ts-generator/run.mjs
```

### 옵션

| 옵션 | 기본값 | 설명 |
|------|--------|------|
| `--repo <path>` | 자동 탐지 | 저장소 루트 경로 |
| `--build` | false | 실행 전 `dotnet run --project framework/cs/Devian.Tools -- build` 호출 |

### 실행 로그 예시

```
=== devian-core-ts-generator ===
  repo: /path/to/devian
  mode: copy-only (no build, use --build to run Devian.Tools)

[1/4] Ensuring package skeleton...
  - framework/ts/devian-core/package.json
  - framework/ts/devian-core/tsconfig.json
  - framework/ts/devian-core/src/index.ts

[2/4] Scanning input artifacts...
  - Common: 4 file(s) from .../modules/ts/Common/generated
  - ws:     1 file(s) from .../modules/ts/ws/generated

[3/4] Copying artifacts...
  - copied 5 file(s)

[4/4] Generating export barrel...
  - framework/ts/devian-core/src/generated/index.g.ts

=== SUCCESS ===
  output: framework/ts/devian-core/src/generated
  files:  5 artifact(s) + index.g.ts
  next:   cd framework/ts/devian-core && npm install && npm run build
```

### 에러 케이스 (입력 없음)

```
[2/4] Scanning input artifacts...
  - Common: 0 file(s) from .../modules/ts/Common/generated
  - ws:     0 file(s) from .../modules/ts/ws/generated

ERROR: No .g.ts files found in modules/ts/**/generated/
  - Ensure 'dotnet run --project framework/cs/Devian.Tools -- build' has been executed
  - Or run this script with --build option
```

---

## Generated Files

### package.json

```json
{
  "name": "devian-core",
  "version": "0.0.0",
  "private": true,
  "type": "module",
  "main": "dist/index.js",
  "types": "dist/index.d.ts",
  "scripts": {
    "build": "tsc -p tsconfig.json",
    "clean": "rm -rf dist"
  },
  "devDependencies": {
    "typescript": "^5.0.0"
  }
}
```

### tsconfig.json

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "declaration": true,
    "outDir": "dist",
    "rootDir": "src",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["src/**/*"]
}
```

### index.g.ts (예시)

```typescript
// AUTO-GENERATED. DO NOT EDIT.
export * from "./common/Table.g";
export * from "./common/TestTableRow.g";
export * from "./common/UserProfile.g";
export * from "./common/UserType.g";
export * from "./ws/ws.g";
```

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | `framework/ts/devian-core/src/generated` 아래에 `common/`, `ws/`가 생성된다 |
| 2 | `framework/ts/devian-core/src/generated/index.g.ts`가 생성된다 |
| 3 | `framework/ts/devian-core/src/index.ts`가 generated 엔트리를 export 한다 |
| 4 | `tsc -p framework/ts/devian-core/tsconfig.json`이 성공한다 |
| 5 | `modules/**`는 변경되지 않는다 |
| 6 | 입력 `.g.ts` 파일이 0개면 **exit code 1**로 실패한다 |
| 7 | `--build` 옵션 여부가 로그에 명확히 출력된다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `10-core-runtime` | **기준** — Core 스펙 정의 |
| `12-common-standard-types` | **참조** — G* 타입 정의 |
| `64-contractgen-implementation` | **소스** — TS contract 생성 |
| `62-protocolgen-implementation` | **소스** — TS protocol 생성 |
| `60-build-pipeline` | **통합** — 빌드 파이프라인 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
