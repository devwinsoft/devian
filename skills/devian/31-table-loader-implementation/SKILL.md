# Devian – 31 Table Loader Implementation

## Purpose

**Table Loader는 Consumer가 제공한 raw data를 파싱하여 typed row로 변환한다.**

> Loader는 파일 시스템/경로/플랫폼을 알지 않는다.  
> Consumer가 raw data를 제공하고, Loader는 파싱만 담당한다.

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| raw → typed row 변환 | 파싱 로직 |
| 도메인 소유 구현체 | `contracts/{language}/{domain}/src/` |

### Out of Scope

| 항목 | 설명 |
|------|------|
| 파일 경로 | Consumer 책임 |
| Addressables / HTTP | Consumer 책임 |
| 플랫폼 특화 코드 | Consumer 책임 |

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | Loader는 파일 시스템/경로를 알지 않는다 |
| 2 | Consumer는 contracts를 **읽는 쪽**이다 |
| 3 | Consumer는 generated 파일을 **소유하지 않는다** |

### Loader가 아는 것 / 모르는 것

| Loader가 아는 것 | Loader가 모르는 것 |
|------------------|-------------------|
| raw data (bytes/string) | 파일 경로 |
| row 타입 구조 | Addressables |
| 파싱 규칙 | HTTP endpoint |
| | 플랫폼 |

---

## Generated Row Types Location

### C# (Always)

```
contracts/csharp/{domain}/generated/
```

### TypeScript (Conditional)

> ⚠️ **If TypeScript codegen is enabled**, generated types are available at:

```
contracts/ts/{domain}/generated/
```

**TS codegen이 비활성화된 경우, 이 경로는 존재하지 않을 수 있다.**

---

## Implementation Location

### C#

| 항목 | 위치 |
|------|------|
| Loader 구현 | `contracts/csharp/{domain}/src/TableLoader/` |
| Generated row | `contracts/csharp/{domain}/generated/` |

### TypeScript (If Enabled)

| 항목 | 위치 |
|------|------|
| Loader 구현 | `contracts/ts/{domain}/src/` |
| Generated row | `contracts/ts/{domain}/generated/` |

---

## Loader Interface (C#)

```csharp
public static class ItemTableLoader
{
    // bytes 입력 - 경로 모름
    public static IReadOnlyList<ItemRow> Load(byte[] bytes);
    
    // string 입력 - 경로 모름
    public static IReadOnlyList<ItemRow> Load(string json);
    
    // RawTableData 입력 - 경로 모름
    public static IReadOnlyList<ItemRow> Load(RawTableData raw);
}
```

---

## Consumer Responsibility

Consumer가 raw data를 제공한다:

```csharp
// Unity Consumer
var bytes = await Addressables.LoadAssetAsync<TextAsset>("items").Result.bytes;
var items = ItemTableLoader.Load(bytes);

// Server Consumer  
var json = File.ReadAllText("tables/items.json");
var items = ItemTableLoader.Load(json);
```

---

## Soft Rules (SHOULD)

| # | Rule |
|---|------|
| 1 | Loader는 순수 파싱만 담당 |
| 2 | 도메인이 Loader 구현을 소유 |
| 3 | 재사용 가능한 파싱 로직 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Loader가 파일 경로/플랫폼을 모름 |
| 2 | TS가 없는 프로젝트에서도 문서가 논리적으로 성립한다 |
| 3 | "Devian은 TS를 필수로 요구한다"는 오해가 발생하지 않는다 |
| 4 | Consumer가 contracts의 **소비자**임이 명확하다 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `21-codegen-table` | Table codegen |
| `30-table-loader-design` | Loader 설계 |
| `32-table-provider-contract` | Provider 계약 |
| `33-consumer-parser-patterns` | Parser 패턴 |
| `35-unity-raw-table-source` | Unity raw fetch |
| `90-language-first-contracts` | 경로 기준 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.3.0 | 2024-12-21 | v5: Hard Rules 추가, TS conditional 명시, Consumer 역할 강화 |
| 0.2.0 | 2024-12-21 | v2: Path sync, loader 경로 무관 명확화 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
