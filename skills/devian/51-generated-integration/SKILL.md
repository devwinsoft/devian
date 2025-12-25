# Devian – 51 Generated Integration

## Purpose

**Generated integration means consuming generated contract artifacts  
inside a domain runtime, not introducing a new integration layer.**

> Generated Integration = contracts 내부에서 generated 코드를 src 런타임이 import해서 쓰는 것  
> **Devian.Core는 generated를 직접 참조하지 않는다**

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| domain runtime에서 generated를 import | 사용 패턴 정의 |
| 경로 기준 | `contracts/{language}/{domain}/generated` |

### Out of Scope (강조)

| 항목 | 설명 |
|------|------|
| Devian-level integration layer | ❌ 존재하지 않음 |
| 자동 등록 / DI | ❌ Devian이 제공하지 않음 |
| Central orchestration | ❌ Devian이 제공하지 않음 |
| core runtime 개입 | ❌ core는 generated를 참조하지 않음 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | core runtime은 generated를 **참조하지 않는다** |
| 2 | generated integration은 **각 domain 내부에서만** 이루어진다 |
| 3 | "integration"은 단순한 **import/사용 패턴**이다 |

### 의존 방향 (Hard)

```
core runtime (extension points)
         ↑
contracts/{language}/{domain}/src (domain runtime)
         ↑
contracts/{language}/{domain}/generated
```

- `core → generated` ❌
- `generated → core` ❌
- **domain runtime만 generated를 import한다**

---

## What "Integration" Means Here

| Integration IS | Integration IS NOT |
|----------------|-------------------|
| domain src에서 generated import | Devian-level 통합 레이어 |
| Consumer가 contracts 사용 | 자동 등록/DI/orchestration |
| 단순한 import 패턴 | 중앙 제어 시스템 |

---

## Generated Code Locations

### C# (Always)

```
contracts/csharp/{domain}/generated/
```

### TypeScript (Optional)

> ⚠️ **If TypeScript codegen is enabled**:

```
contracts/ts/{domain}/generated/
```

**TS codegen이 비활성화된 경우, 이 경로는 존재하지 않을 수 있다.**

---

## Hand-written 영역

### C#

```
contracts/csharp/{domain}/src/
```

### TypeScript (If Enabled)

```
contracts/ts/{domain}/src/
```

---

## Integration Pattern (Example)

### Domain Runtime에서 Generated Import

```csharp
// contracts/csharp/ingame/src/ItemService.cs
using Ingame.Generated;  // ← generated import

public class ItemService
{
    public ItemRow GetItem(int id)
    {
        // generated 타입 사용
    }
}
```

### Consumer에서 Domain Runtime 사용

```csharp
// Unity App
var service = new ItemService();
var item = service.GetItem(123);
```

---

## What Generated Code Must NOT Do

| 금지 | 설명 |
|------|------|
| transport 코드 포함 | ❌ |
| NestJS/Unity 특화 코드 포함 | ❌ |
| core runtime 확장 시도 | ❌ |
| 수동 수정 | ❌ (재생성 가능해야 함) |

**generated의 역할은 "형태(shape)"로 제한한다.**

---

## Integration Lifecycle

### Build Time

```
input 변경 → Devian.Tools 실행 → contracts/{language}/{domain}/generated 갱신
```

### Runtime

Consumer가 해당 언어 모듈을 가져다 사용:

| Language | 방식 |
|----------|------|
| C# | 프로젝트 참조/패키지 참조 |
| TS | workspace/git/publish 등 (강제하지 않음) |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | "integration"이라는 단어가 **프레임워크/중앙 레이어로 오해되지 않는다** |
| 2 | generated integration이 **단순한 import/사용 패턴**임이 명확하다 |
| 3 | core runtime은 generated를 **참조하지 않음**이 명시되어 있다 |
| 4 | TS가 없는 프로젝트에서도 문서가 논리적으로 성립한다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `01-devian-core-philosophy` | 철학 기준 |
| `10-core-runtime` | Core 책임 범위 |
| `20-codegen-protocol` | Protocol codegen |
| `21-codegen-table` | Table codegen |
| `26-domain-scaffold-generator` | 도메인 뼈대 |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-21 | v5: Purpose 재정의, core→generated 금지 명시, Out of scope 강화 |
| 0.2.0 | 2024-12-21 | v2: Language-first contracts |
| 0.1.0 | 2024-12-21 | Initial skill definition |
