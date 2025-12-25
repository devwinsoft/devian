# Devian – 33 Consumer Parser Patterns

## Purpose

**Consumer가 테이블/프로토콜 데이터를 파싱하는 패턴을 정의한다.**

> Consumer는 contracts의 **소비자**다.  
> Consumer는 generated를 import할 뿐, contracts 구조를 변경하지 않는다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| Parser 패턴 | Consumer 측 파싱 로직 |
| Registry 패턴 | 파서 등록/관리 |
| 언어별 구현 위치 | C#, TypeScript (optional) |

### Out of Scope

| 항목 | 설명 |
|------|------|
| 파서 등록 시점 | Consumer가 결정 |
| 레지스트리 구조 | Consumer가 결정 |
| 파싱 전략 | strict/lenient 등 Consumer 선택 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Consumer는 generated를 **import만** 한다 |
| 2 | Consumer는 contracts 구조를 **변경하지 않는다** |
| 3 | Consumer는 core/runtime으로 오인되어서는 안 된다 |

### 의존 방향 (Hard)

```
core runtime (extension points)
         ↑
    Consumer (parsers, loaders)
         ↑
contracts/{language}/{domain}/generated
```

- Consumer는 generated를 **소비**한다
- Consumer는 generated를 **소유하지 않는다**

---

## Parser Location

### C#

```
contracts/csharp/{domain}/src/Parsing/
```

### TypeScript (If Enabled)

> ⚠️ **If TypeScript codegen is enabled**:

```
contracts/ts/{domain}/src/
```

**TS codegen이 비활성화된 경우, 이 경로는 존재하지 않을 수 있다.**

---

## Parser Registry Pattern (Example)

### C#

```csharp
// contracts/csharp/ingame/src/Parsing/ParserRegistry.cs
public static class InGameParserRegistry
{
    public static void Register()
    {
        CoreValueParserRegistry.Register<ItemId>(new ItemIdParser());
        CoreValueParserRegistry.Register<GFloat3>(new GFloat3Parser());
    }
}
```

### TypeScript (If Enabled)

```typescript
// contracts/ts/ingame/src/parsers.ts
export const parsers = {
  itemId: (raw: string) => ({ value: raw }),
  gFloat3: (raw: string) => parseGFloat3(raw),
};
```

---

## Consumer Integration

Consumer가 도메인 파서를 초기화한다:

```csharp
// Unity/Server 앱 시작 시
InGameParserRegistry.Register();
```

```typescript
// NestJS 서비스 (if TS enabled)
import { parsers } from '@devian/ingame';
```

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | 파서는 도메인 런타임이 구성 |
| 2 | 등록 방식은 Consumer가 결정 |
| 3 | C#/TS 동일 패턴 가능 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | C# consumer가 core/runtime으로 오인되지 않는다 |
| 2 | tablegen / scaffold와 책임이 섞이지 않는다 |
| 3 | TS가 없는 프로젝트에서도 문서가 논리적으로 성립한다 |
| 4 | Consumer가 generated의 **소비자**임이 명확하다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `21-codegen-table` | Table codegen |
| `30-table-loader-design` | Loader 설계 |
| `31-table-loader-implementation` | Loader 구현 |
| `32-table-provider-contract` | Provider 계약 |
| `51-generated-integration` | Generated 통합 |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-21 | v5: Hard Rules 추가, Consumer 역할 명확화, TS conditional |
| 0.2.0 | 2024-12-21 | v2: Path sync, 규칙 강제 제거 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
