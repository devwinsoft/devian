# Devian – 35 Unity Raw Table Source

## Purpose

**Unity 환경에서 테이블 데이터를 "가져오기만" 하는 역할을 정의한다.**

Unity는 오직 **raw 데이터(source)를 fetch**하고,  
파싱/로딩/매핑은 전부 Domain 책임이다.

---

## Belongs To

**Unity Skill**

> Unity Skill은 **Devian Framework의 확장**이다.  
> Unity가 없더라도 Devian Framework는 완전하다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Unity에서 raw table source를 읽는 패턴 | Addressables, TextAsset |
| raw payload 반환 | `string`, `byte[]` |

### Out of Scope (다른 Skill/Framework 영역)

| 항목 | 담당 |
|------|------|
| contracts 정의 | Framework |
| codegen | Tools |
| core runtime 확장 | Core |
| server architecture | Server Skill |
| NestJS modules | Server Skill |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Unity 관련 내용은 **Unity Skill**이다 |
| 2 | **Framework 규약을 변경하지 않는다** |
| 3 | Unity module은 domain contracts를 **참조하지 않는다** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Unity module은 raw fetch만 담당 |
| 2 | 파싱/로딩 규칙은 domain에서 처리 |
| 3 | 의도적으로 "기능이 부족해 보일 정도로" 얇게 유지 |

---

## Implementation Location

```
skills/
└── engine/
    └── unity/
        └── Devian.Unity/
            └── Assets/Devian/Runtime/
```

> ⚠️ `packages/` 용어는 사용하지 않는다 (Unity `Packages/`와 혼동 방지)

이 모듈은:
- Unity Skill 전용
- asmdef 분리
- 선택적 바인딩

---

## Dependency Rules

### Unity module CAN reference

| Target | Location | Allowed |
|--------|----------|:-------:|
| `Devian.Common` | `framework/cs/Devian.Common/` | ✅ |
| `Devian.Core` | `framework/cs/Devian.Core/` | ✅ (최소) |
| `UnityEngine` | Unity 내장 | ✅ |

### Unity module MUST NOT reference

| Target | Forbidden |
|--------|:---------:|
| `contracts/csharp/{domain}` | ❌ |
| 도메인 row 타입/loader/parser | ❌ |

---

## Responsibility Boundary

### Unity Table Asset Source DOES

- Addressables / Resources / TextAsset / StreamingAssets에서:
  - `string`
  - `byte[]`
  - `ReadOnlyMemory<byte>`
  를 **가져온다**
- 로딩 성공/실패 상태를 전달한다

### Unity Table Asset Source DOES NOT

| 금지 항목 | 설명 |
|----------|------|
| 헤더 파싱 | ❌ |
| row 생성 | ❌ |
| 스키마 해석 | ❌ |
| 검증 | ❌ |
| variant merge | ❌ |

---

## Integration Pattern (Recommended)

### Step 1) Unity side

- Unity code에서 raw table asset을 load
- 결과를 "도메인 provider 구현체"에 전달

### Step 2) Domain side

- 도메인에서 `ITableProvider` 구현
- Unity source를 내부적으로 사용하여 raw payload 획득
- 이후 모든 로딩/파싱/검증은 도메인 로더에서 수행

---

## Responsibilities

1. **Raw fetch 패턴 제공** — Unity 환경용 Skill 구현
2. **Domain 분리 유지** — Unity는 domain을 모름
3. **Unity Skill 명시** — Framework 규약을 변경하지 않음

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | **Unity가 없더라도 Devian Framework는 완전하다** |
| 2 | **Unity Skill**로 명확히 인식된다 |
| 3 | Unity module이 raw fetch 역할만 수행한다 |
| 4 | 도메인 파싱/로딩 규칙이 Unity로 새어 나오지 않는다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `32-table-provider-contract` | Provider 계약 (도메인) |
| `30-table-loader-design` | 로더 설계 (도메인) |
| `31-table-loader-implementation` | 로더 구현 (도메인) |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-12-25 | **디렉토리 정책**: `packages/` → `framework/`, Unity Skill로 재정의 |
| 0.3.0 | 2024-12-21 | 표준 템플릿 적용 |
| 0.2.0 | 2024-12-21 | Raw fetch only 명확화 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
