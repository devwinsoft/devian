# Devian – 21 Codegen: Table Schema & Row Types

## Purpose

**테이블 스펙과 원천(XLSX)을 기반으로, 언어별 코드/데이터 산출물을 생성한다.**

이 문서는 "고정된 contracts 레이아웃"이 아니라, **`build.json`의 output 템플릿**을 따른다.

---

## Belongs To

**Consumer Tooling (Build / Codegen)**

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| `input/tables/{domain}/*.xlsx` 변환 | `{DATA_OUT}/{domain}/*.json` |
| 테이블 행 타입/스키마 타입 생성 | C# / TS (optional) |
| C# 출력 | `{CS_OUT}/{domain}/Runtime/generated/*.cs` |
| TS 출력 (optional) | `{TS_OUT}/{domain}/generated/*.ts` |

### Out of Scope

| 항목 | 설명 |
|------|------|
| Unity AssetBundle/Addressables | ❌ consumer 영역 |
| 서버/클라이언트 아키텍처 | ❌ consumer 영역 |
| 런타임 로더 구현 | ❌ 30/31 참고 |

---

## Inputs (Source of truth)

| Input | 설명 |
|-------|------|
| `input/build/build.json` | 빌드 설정, 출력 경로 템플릿 |
| `input/tables/{domain}/*.xlsx` | 테이블 원천 데이터 |
| `input/contracts/{domain}/*.json` | (옵션) 공통 타입/enum 정의 |

---

## Outputs

> ⚠️ **출력 경로는 반드시 `build.json`의 템플릿을 따른다.**  
> Devian은 `generated/` 폴더명을 강제하지 않는다.

| 타겟 | 출력 경로 템플릿 |
|------|-----------------|
| Data | `{DATA_OUT}/{domain}/*.json` |
| C# | `{CS_OUT}/{domain}/Runtime/generated/*.cs` |
| TypeScript (optional) | `{TS_OUT}/{domain}/generated/*.ts` |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 생성기는 output 템플릿에 **그대로** 쓴다 (폴더명/계층을 재해석하지 않는다) |
| 2 | TypeScript 출력은 **optional**이며, 도메인별 `emit.ts`로 제어된다 |
| 3 | 같은 input → 같은 output (결정적 빌드) |
| 4 | core runtime은 generated 코드를 **직접 참조하지 않는다** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | XLSX → JSON 변환은 temp workspace에서 수행하고, 최종만 output에 쓴다 |
| 2 | 생성 파일 상단에 `DO NOT EDIT` 헤더를 넣는다 |

---

## Dependency Direction

```
core runtime (extension points)
    ↑
Consumer (parsers, loaders)
    ↑
{CS_OUT}/{domain}/generated
```

> Consumer가 generated를 import한다.  
> core runtime은 generated를 모른다.

---

## Responsibilities

1. **테이블 원천을 런타임 친화적 데이터(JSON)로 변환**
2. **소비자 언어(C#/TS)에서 사용할 타입/헬퍼 코드 생성**
3. **build.json 템플릿 기반 출력 경로 준수**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | `build.json`을 바꾸면 출력 위치가 즉시 바뀐다 (경로 하드코딩 없음) |
| 2 | TS 타겟을 끄면 TS 출력이 생성되지 않는다 |
| 3 | Unity `Runtime/` 요구사항을 출력 경로에서 그대로 반영한다 |
| 4 | core runtime이 generated 코드를 직접 참조하지 않는다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | Build Spec |
| `24-table-authoring-rules` | Authoring 규칙 |
| `30-table-loader-design` | Loader 설계 |
| `31-table-loader-implementation` | Loader 구현 |
| `61-tablegen-implementation` | Tablegen 구현 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.4.0 | 2024-12-21 | build.json 템플릿 기반, output 강제 금지 |
| 0.3.0 | 2024-12-21 | 의존 방향, TS optional |
| 0.2.0 | 2024-12-21 | C# contracts output |
| 0.1.0 | 2024-12-20 | Initial skill definition |
