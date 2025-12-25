# Devian – 32 Table Provider Contract

## Purpose

**테이블 데이터를 런타임에서 읽어오는 계약(Contract)을 정의한다.**

Provider contract는 **interface only**이며, 구현을 포함하지 않는다.

---

## Belongs To

**Contracts**

> Provider contract는 contracts 수준의 **인터페이스 정의**다.  
> 구현은 **Skill**에서 담당한다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Table 제공자(provider)의 계약 | `ITableProvider` 인터페이스 |
| Skill과의 경계 정의 | raw 데이터 획득 책임 |

### Out of Scope (Skill 영역)

| 항목 | 담당 Skill |
|------|-----------|
| provider 구현 | 각 플랫폼 Skill |
| storage 방식 | 각 플랫폼 Skill |
| runtime 로딩 전략 | 도메인 Skill |
| server architecture | Server Skill |
| generated code ownership | 도메인 소유 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Provider contract는 **interface only**다 |
| 2 | 구현을 **포함하지 않는다** |
| 3 | Framework(`framework/*`)는 domain contracts를 **참조하지 않는다** |

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Provider는 raw 데이터만 책임 |
| 2 | 파싱/매핑은 도메인 로더 책임 |
| 3 | variant(`ITEM@weapon`) 정책을 TableId 수준에서 표현 |

---

## Where This Contract Lives

### Domain location (canonical)

```
contracts/csharp/{domain}/src/TableProviderContract.cs
```

또는 도메인별로 파일 분리:

```
contracts/csharp/{domain}/src/TableProvider/*.cs
```

---

## Minimal Types (Recommended)

> 네이밍은 도메인에서 조정 가능.

### TableId

테이블을 식별하기 위한 최소 키:
- `Name`: 테이블명 (예: `ITEM`, `SOUND`)
- `Variant`: 선택 (예: `weapon`, `armor`)

### TableLoadRequest

로딩 요청 단위:
- `TableId id`
- `TableLoadMode mode` (Dev/Release)
- `TableFormat format` (Json/MsgPack)

### TablePayload (Raw)

Provider가 반환하는 결과:
- `ReadOnlyMemory<byte> data` 또는 `string text`

### ITableProvider

```csharp
public interface ITableProvider
{
    TablePayload? TryGet(TableLoadRequest request);
    // 또는 Task<TablePayload?> GetAsync(...)
}
```

---

## Separation of Responsibilities

### Provider contract responsibility

- raw 데이터 획득
- 테이블 단위/변형 단위 식별
- 캐시 (선택)

### NOT provider responsibility

- 테이블 헤더 파싱
- row 타입 생성/매핑
- value parser registry 적용
- strict/lenient 검증 상세

---

## Dependency Rules

### Allowed

- `contracts/csharp/{domain}` → `common` 도메인 ✅
- `contracts/csharp/{domain}` → `Devian.Core` (`framework/cs/`) ✅

### Forbidden

- Framework (`framework/*`) → `contracts/csharp/{domain}` ❌
- UnityEngine 의존성 → contracts ❌

---

## Responsibilities

1. **Provider 계약 정의** — interface only
2. **Skill과 경계 분리** — raw 데이터 획득 책임 명확화
3. **구현 분리** — 구현은 domain/Skill에서

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Provider = 구현체라는 오해가 없다 |
| 2 | Skill/runtime 쪽 문서와 역할이 겹치지 않는다 |
| 3 | Provider 계약이 `contracts/csharp/{domain}`에 위치한다 |
| 4 | Provider는 raw 데이터만 책임, 파싱은 도메인 로더 책임 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `30-table-loader-design` | 도메인 로더 설계 |
| `31-table-loader-implementation` | 도메인 로더 구현 |
| `33-consumer-parser-patterns` | 파서 패턴 |
| `35-unity-raw-table-source` | Unity raw source |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-12-25 | **디렉토리 정책**: `packages/` → `framework/`, consumer → Skill |
| 0.3.0 | 2024-12-21 | 표준 템플릿 적용, interface only 강조 |
| 0.2.0 | 2024-12-21 | 도메인 소유로 변경 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
