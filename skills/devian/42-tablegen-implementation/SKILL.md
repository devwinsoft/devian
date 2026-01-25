# 42-tablegen-implementation

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Table generator의 **정책적 규칙**만 정의한다.

구체적인 구현/산출 API는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Naming Rules (정책)

### Entity 클래스/인터페이스

- **SheetName을 그대로 사용**한다.
- 예: Sheet `TestSheet` → C# `class TestSheet`, TS `interface TestSheet`

### Container 클래스

- C#: `TB_{SheetName}` (namespace 바로 아래, static class)
- TS: `TB_{SheetName}` (최상위, static 멤버)
- 예: Sheet `TestSheet` → `TB_TestSheet`

**PrimaryKey(`key:true`)가 없는 sheet는 Container 클래스를 생성하지 않는다.**

- PrimaryKey가 있는 sheet → `TB_{SheetName}` 생성
- PrimaryKey가 없는 sheet → Container 생성 안함 (Entity만 생성)

### Namespace (C#)

- `Devian.Domain.{DomainKey}`를 사용한다.
- 예: Domain `Common` → `namespace Devian.Domain.Common`

---

## IEntity Interface (정책)

### 인터페이스 정의

```csharp
// C#
public interface IEntity { }
public interface IEntityKey<T> : IEntity { T GetKey(); }
```

```typescript
// TypeScript
export interface IEntity { }
export interface IEntityKey<T> extends IEntity { getKey(): T; }
```

### 적용 규칙

| 유형 | 예시 | 상속 | GetKey |
|------|------|------|--------|
| Contract class | `UserProfile` | `IEntity` | ❌ 없음 |
| Table Entity (Key 있음) | `TestSheet` | `IEntityKey<KeyType>` | ✅ 있음 |
| Table Entity (Key 없음) | `VECTOR3` | `IEntity` | ❌ 없음 |

### 생성 예시

**C#:**
```csharp
// Contract - IEntity만 상속
public sealed class UserProfile : IEntity
{
    public int Id { get; set; }
    // GetKey 없음
}

// Table with Key - IEntityKey<T> 상속
public sealed class TestSheet : IEntityKey<int>
{
    public int Number { get; set; }
    public int GetKey() => Number;
}

// Table without Key - IEntity만 상속
public sealed class VECTOR3 : IEntity
{
    public float X { get; set; }
    // GetKey 없음
}
```

**TypeScript:**
```typescript
// Contract - IEntity만 extends
export interface UserProfile extends IEntity {
    id: number;
    // getKey 없음
}

// Table with Key - IEntityKey<T> extends
export interface TestSheet extends IEntityKey<number> {
    number: number;
    getKey(): number;
}

// Table without Key - IEntity만 extends
export interface VECTOR3 extends IEntity {
    x: number;
    // getKey 없음
}
```

---

## Output Files (정책)

Domain 내 모든 Contract, Table Entity, Table Container는 **단일 파일에 통합** 생성된다.

| 산출물 | 파일명 패턴 | 예시 | 생성 조건 |
|--------|-------------|------|-----------|
| C# (통합) | `{DomainKey}.g.cs` | `Common.g.cs` | 항상 |
| TS (통합) | `{DomainKey}.g.ts` | `Common.g.ts` | 항상 |
| NDJSON Data | `{SheetName}.ndjson` | `TestSheet.ndjson` | PrimaryKey 있는 sheet만 |

---

## Common Dependency (Hard Rule)

DATA Domain 모듈은 Common 참조 여부를 판정하지 않는다.

- `{DomainKey} != Common`인 모든 DATA Domain 모듈은 ``Devian` + `.Module.Common`` / `@devian/module-common`을 **무조건** 참조한다.
- Common 모듈 자기 자신은 자기 자신을 참조하지 않는다.

필수 적용:

- C#: `{csConfig.generateDir}/` + `Devian` + `.Module.{DomainKey}` + `/` + `Devian` + `.Module.{DomainKey}.csproj`는 `Devian` + `.Module.Common`을 ProjectReference로 포함해야 한다.
- TS: `@devian/module-{domainkey}`의 `package.json` `dependencies`에 `@devian/module-common`을 포함해야 한다.

---

## Unity Compatibility (Hard Rule)

Unity 환경에서의 호환성을 위해 다음 규칙을 강제한다.

**C# Domain 코드 생성 시:**

1. **System.Text.Json 사용 금지**
   - Unity는 `System.Text.Json`을 기본 제공하지 않음
   - `using System.Text.Json;` 생성 금지
   - `JsonDocument`, `JsonSerializer` 등 사용 금지

2. **Newtonsoft.Json 사용**
   - Unity는 `Newtonsoft.Json`을 기본 제공 (com.unity.nuget.newtonsoft-json)
   - `using Newtonsoft.Json;` 사용
   - `JsonConvert.DeserializeObject<T>()` 사용

**UPM package.json 종속성:**

생성 코드가 Newtonsoft.Json을 사용하므로, UPM package.json 생성 시 종속성도 함께 추가한다.

```json
{
  "name": "com.devian.domain.{domain}",
  "dependencies": {
    "com.devian.core": "1.0.0",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

- 빌드 시스템이 Domain UPM scaffold 생성 시 자동으로 `com.unity.nuget.newtonsoft-json` 종속성 추가
- 코드와 종속성 일치 보장

### 파일 구조 (C#)

```csharp
namespace Devian.Domain.{DomainKey}
{
    // Contracts (enum, class : IEntity)
    // Table Entities (class : IEntity 또는 IEntityKey<T>)
    // Table Containers (static class TB_*)
}
```

### 파일 구조 (TS)

```typescript
import { IEntity, IEntityKey } from '@devian/core';

// Contracts (enum, interface extends IEntity)
// Table Interfaces (extends IEntity 또는 IEntityKey<T>)
// Table Containers (export class TB_*)
```

---

## Container API (정책)

> Container(`TB_{SheetName}`)는 PrimaryKey가 있는 sheet에 대해서만 생성된다.

### C# Container (static class TB_{SheetName})

| 메서드 | 설명 |
|--------|------|
| `Count` | Row 개수 |
| `Clear()` | 캐시 비우기 |
| `GetAll()` | 전체 Row 반환 |
| `Get(key)` | Key로 조회, nullable 반환 (Key 있는 경우) |
| `TryGet(key, out row)` | Key로 조회, out 패턴 (Key 있는 경우) |
| `LoadFromJson(json)` | JSON 배열 로드 |
| `LoadFromNdjson(ndjson)` | NDJSON 로드 |

### TS Container (export class TB_{SheetName})

| 메서드 | 설명 |
|--------|------|
| `count` | Row 개수 |
| `clear()` | 캐시 비우기 |
| `getAll()` | 전체 Row 반환 |
| `get(key)` | Key로 조회 (Key 있는 경우) |
| `has(key)` | Key 존재 여부 (Key 있는 경우) |
| `loadFromJson(json)` | NDJSON 로드 |
| `saveToJson()` | NDJSON 저장 |

---

## Implementation is Code Truth

아래 항목은 SKILL이 아니라 **코드가 정답**이다.

- 생성되는 C#/TS 코드의 구체적 구조, 메서드, 프로퍼티
- Entity/Container의 정확한 인터페이스 시그니처
- JSON 파싱/직렬화 구현
- NDJSON 필드 순서, 값 변환 규칙

SKILL은 위 내용을 단정해서는 안 된다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 입력 규칙: `skills/devian/30-table-authoring-rules/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
