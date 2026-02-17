# 42-tablegen-implementation

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian-core/03-ssot/SKILL.md

## Purpose

Table generator의 **정책적 규칙**만 정의한다.

구체적인 구현/산출 API는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## Naming Rules (정책)

### Sheet Name @ 규칙 (Hard Rule)

Sheet 이름이 `{TableName}@{Description}` 형식일 때:

| 항목 | 값 |
|------|-----|
| SheetFullName | `{TableName}@{Description}` (원본) |
| CodeTableName | `@` 앞 문자열 (`{TableName}`) |

**코드 생성에 사용되는 이름은 CodeTableName만이다.**

```
예: Sheet "Monsters@몬스터테이블"
→ CodeTableName = "Monsters"
→ C# class = Monsters
→ Container = TB_Monsters
→ Runtime에서 로드 시 TB_Monsters에 insert
```

### 중복 codeTableName FAIL 규칙 (Hard Rule)

**같은 XLSX 파일 내에서 동일한 codeTableName을 가진 시트가 2개 이상 있으면 빌드 FAIL.**

```
예: FAIL 케이스
- Sheet1: "Monsters@보스몬스터"
- Sheet2: "Monsters@일반몬스터"
→ codeTableName이 둘 다 "Monsters" → FAIL
```

에러 메시지에는 XLSX 파일명과 충돌하는 시트 이름 목록이 포함되어야 한다.

### Entity 클래스/인터페이스

- **CodeTableName을 그대로 사용**한다.
- 예: Sheet `TestSheet` → C# `class TestSheet`, TS `interface TestSheet`
- 예: Sheet `Monsters@몬스터테이블` → C# `class Monsters`

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
| NDJSON Data | `{SheetName}.json` | `TestSheet.json` | PrimaryKey 있는 sheet만 |
| pb64 Data | `{SheetName}.asset` | `TestSheet.asset` | PrimaryKey 있는 sheet만 |

> **NDJSON 저장 규약:** 내용은 NDJSON이며 확장자는 `.json` 고정. 정본: `skills/devian-builder/34-ndjson-storage/SKILL.md`
> **pb64 저장 규약:** Unity TextAsset `.asset` 형식. 정본: `skills/devian-builder/35-pb64-storage/SKILL.md`

---

## Domain 모듈 의존성 (Hard Rule)

**C# DATA Domain 모듈 의존성:**
- `Devian.Domain.{DomainKey}.csproj`는 `..\Devian\Devian.csproj`만 ProjectReference 한다.
- Common 모듈을 포함한 모든 Domain 모듈이 동일한 규칙을 따른다.

**TS DATA Domain 패키지 의존성:**
- `@devian/module-{domainkey}`는 `@devian/core`만 의존한다.

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
    "com.devian.foundation": "0.1.0",
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
| `_AfterLoad()` | AfterLoad 훅 (internal, 아래 섹션 참조) |

**Group API (group:true 컬럼 있을 때):**

| 메서드 | 설명 |
|--------|------|
| `GetGroupKeys()` | 중복 제거된 groupKey 리스트 |
| `GetByGroup(groupKey)` | groupKey에 속한 row 리스트 |
| `TryGetGroupPrimaryKey(groupKey, out key)` | groupKey의 대표 PK(min PK) 반환 |
| `TryGetGroupKeyByKey(key, out groupKey)` | PK로 groupKey 역조회 |

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

## AfterLoad Hook Contract (Hard Rule)

빌더는 TableManager가 TB insert를 수행한 직후, 각 TB 컨테이너에 대해 AfterLoad 훅을 호출한다.

- 빌더는 `TB_*` insert 직후 반드시 `TB_*._AfterLoad()`를 호출해야 한다.
- `TB_*` 컨테이너는 항상 다음을 제공해야 한다:
  - `internal static void _AfterLoad()` (항상 존재)
  - `static partial void _OnAfterLoad()` (옵션 훅, 구현 없으면 no-op)
- `_AfterLoad()`는 내부에서 `_OnAfterLoad()`를 호출한다.

이 훅은 "인덱스 빌드/캐시 초기화/도메인 내부 후처리"를 **결정적으로** 수행하기 위한 표준 진입점이다.

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

- Policy SSOT: `skills/devian-core/03-ssot/SKILL.md`
- 입력 규칙: `skills/devian-builder/30-table-authoring-rules/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
