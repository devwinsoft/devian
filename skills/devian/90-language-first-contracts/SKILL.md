# Devian – 90 Language-First Contracts

## Purpose

**Devian은 언어별 산출물 분리(language-first)를 기본으로 권장한다.**

다만 Devian은 폴더명을 강제하지 않으며, **최종 경로는 build.json이 결정**한다.

> "language-first"는 **고정 레이아웃 강제**가 아니라, **타겟 분리 원칙**이다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| 한 도메인에서 C#/TS/Data 동시 생성 | 또는 선택적 생성 |
| 언어별로 분리된 산출물 | 최소 규칙 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| `contracts/{language}/{domain}` 고정 레이아웃 강제 | ❌ |
| 패키징 방식(nuget/npm) 강제 | ❌ |
| 서버/Unity 프로젝트 구조 강제 | ❌ |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | 언어별 출력은 **타겟(cs/ts/data)** 단위로 분리된다 |
| 2 | 출력 경로는 `build.json`의 템플릿(`targets.*.output`)을 **그대로 따른다** |
| 3 | TypeScript 타겟은 **optional**일 수 있다 (도메인별 emit 제어 허용) |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 권장 레이아웃: `{CS_OUT}/{domain}/...`, `{TS_OUT}/{domain}/...`, `{DATA_OUT}/{domain}/...` |
| 2 | Unity 요구사항(예: `Runtime/`)이 있으면 템플릿에 그대로 반영한다 |

---

## Why "Principle" Not "Layout"

기존의 `contracts/{language}/{domain}` 고정 레이아웃은:
- Unity asmdef 제약과 충돌할 수 있음
- 팀마다 다른 폴더 규칙과 충돌할 수 있음
- 유연성이 부족함

새로운 **타겟 분리 원칙**은:
- `build.json`이 경로를 결정
- 팀/엔진 규칙을 그대로 수용
- C#/TS/Data가 섞이지 않음을 보장

---

## Responsibilities

1. **"language-first"를 강제가 아닌 산출물 분리 원칙으로 설명**
2. **build.json 기반 output 템플릿 설계와 충돌하지 않게 유지**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | 문서 어디에서도 고정된 폴더명(예: `generated/`)을 **강제하지 않는다** |
| 2 | C#/TS/Data 분리 원칙이 빌드 스펙으로 구현 가능하다 |
| 3 | `contracts/` 폴더가 필수가 아님이 명확하다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `60-build-pipeline` | Build Spec |
| `21-codegen-table` | Table codegen |
| `28-json-row-io` | JSON I/O 정본 |
| `00-rules-minimal` | Hard Rules |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-12-28 | Initial |
