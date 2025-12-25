# Devian – 30 Table Loader Design

## Purpose

**소비자 런타임에서 JSON 테이블 데이터를 "class 인스턴스 캐시"로 로드하는 설계 가이드를 제공한다.**

이 문서는 Fetch(JSON 획득)와 Load(캐시 생성)의 **책임 분리**를 명확히 한다.

---

## Belongs To

**Consumer / Application**

---

## Key Concepts

| 개념 | 설명 |
|------|------|
| **ExcelFileName** | `A.xlsx`의 `A`. 생성 파일/LoadFile 함수의 기준 |
| **SheetName** | Excel 시트 이름. 각 시트가 별도의 테이블 |
| **TableName** | 테이블 식별자. 기본값 = SheetName (정규화됨) |
| **sheetKey** | `loader.Load(sheetKey)`에 전달되는 정규화된 키 |
| **ILoaderTextJson** | JSON string을 반환하는 loader 인터페이스 (Devian 제공) |
| **Fetch** | JSON을 가져오는 행위 (Unity/BundleManager 책임) |
| **Load** | JSON → class 인스턴스 캐시 생성 (Devian Loader 책임) |

---

## Table Header Structure (원본 Excel) — 3줄 고정

```
Row 1: Field Name    (필드 이름)
Row 2: Type          (타입, prefix 지원)
Row 3: Comment       (순수 설명용 주석 — Devian은 절대 해석하지 않음)
Row 4+: Data         (실제 데이터)
```

> **IMPORTANT:**
> - Comment (3행)은 JSON에 포함되지 않는다.
> - **Devian은 3행을 절대 해석하지 않는다** — tablegen, validator, runtime 모두 해당.
> - 3행에 meta/option/policy/constraint 개념은 없다.

---

## Primary Key Rules

| # | Rule |
|---|------|
| 1 | **Primary Key는 항상 1열** |
| 2 | **Key 타입: `class:*` 금지** |
| 3 | **Key 타입: 배열 타입 금지** |
| 4 | 허용: `int`, `string`, `enum:*` |

---

## Type Support

| 타입 | 설명 |
|------|------|
| `int`, `float`, `string`, `bool` | primitives |
| `datetime`, `text` | 확장 primitives |
| `int[]`, `string[]` | 배열 |
| `enum:Name` | contracts 열거형 참조 |
| `class:Name` | contracts 클래스 참조 |
| `ref:Name` | **예약됨 (미구현)** |

---

## Core Structure

```
Excel 파일 1개 = 시트 N개 = 테이블 N개

A.xlsx
├── Sheet "Items"    → T_Items,    sheetKey: "items"
├── Sheet "Weapons"  → T_Weapons,  sheetKey: "weapons"
└── Sheet "Armors"   → T_Armors,   sheetKey: "armors"

→ Table.A.g.cs
   └── public static partial class Table
       ├── LoadFile_A(ILoaderTextJson loader)
       ├── UnloadFile_A()
       ├── T_Items
       ├── T_Weapons
       └── T_Armors
```

---

## Loader Interface (Devian 제공)

```csharp
namespace Devian.Tables
{
    public interface ILoaderTextJson
    {
        string Load(string key);
    }
}
```

- **Devian이 인터페이스 정의**
- **Unity 프로젝트(BundleManager)가 구현**

---

## Hard Rules (MUST)

| # | Rule |
|---|------|
| 1 | **Devian Loader는 Fetch를 하지 않는다** — `ILoaderTextJson.Load(key)`로 JSON을 받음 |
| 2 | **JSON을 어디서 얻는지는 Unity 프로젝트 책임** |
| 3 | **캐시는 class 인스턴스만 보관** — 원본 JSON string, byte[], TextAsset 저장 금지 |
| 4 | **UnloadFile은 인스턴스 캐시만 제거** — asset release는 loader 책임 |
| 5 | **Primary Key는 항상 1열** |
| 6 | **Key 타입: class:*, 배열 금지** |
| 7 | **Comment(3행)는 JSON 미포함, Devian은 절대 해석하지 않음** |
| 8 | **ref: 타입은 미지원 (planned)** |
| 9 | **Namespace 고정:** `Devian.Tables` |
| 10 | **Addressables/AssetBundle 정책을 다루지 않는다** |
| 11 | **provider/router 등 과설계 금지** — 하드코딩 LoadFile이 표준 |

---

## Standard API Pattern

### ExcelFileName 단위

```csharp
public static void LoadFile_{ExcelFileName}(ILoaderTextJson loader);
public static void UnloadFile_{ExcelFileName}();
```

### 개별 테이블

```csharp
public static class T_{TableName}
{
    public static bool IsLoaded { get; }
    public static void LoadFromJson(string jsonArrayText);
    public static {RowType} Get({KeyType} key);
    public static bool TryGet({KeyType} key, out {RowType}? row);
    public static void Unload();
}
```

---

## Loader Flow (책임 분리)

```
Unity Project (BundleManager)
    │
    │ Addressables / AssetBundle / Resources
    │ (Unity 책임 — Devian은 모름)
    │
    ↓
ILoaderTextJson 구현
    │
    │ loader.Load("items") → JSON string
    │
    ↓
Devian Loader
    │
    │ LoadFile_A(loader)
    │   → T_Items.LoadFromJson(loader.Load("items"))
    │   → T_Weapons.LoadFromJson(loader.Load("weapons"))
    │
    ↓
Runtime Usage
    │
    │ T_Items.Get(key)
    │
    ↓
UnloadFile_A()
    │
    │ T_Items.Unload()
    │ T_Weapons.Unload()
    │ (캐시만 제거, asset release는 loader 책임)
```

---

## Usage Example

```csharp
// Unity 프로젝트: ILoaderTextJson 구현
public class BundleTableLoader : ILoaderTextJson
{
    private readonly string _basePath;
    
    public BundleTableLoader(string basePath) => _basePath = basePath;
    
    public string Load(string key)
    {
        return BundleManager.LoadText($"{_basePath}/{key}.json");
    }
}

// 사용
var loader = new BundleTableLoader("Data/common");

// 로딩 (Excel 파일 단위)
Devian.Tables.Table.LoadFile_A(loader);

// 조회
var item = Devian.Tables.Table.T_Items.Get(1001);
var weapon = Devian.Tables.Table.T_Weapons.Get(2001);

// 언로딩
Devian.Tables.Table.UnloadFile_A();
```

---

## What Devian Does NOT Store

| 항목 | 저장 여부 |
|------|----------|
| Row 인스턴스 | ✅ `Dictionary<TKey, Row>` |
| JSON string | ❌ |
| byte[] | ❌ |
| TextAsset | ❌ |
| AssetBundle handle | ❌ |
| Addressables handle | ❌ |

---

## Scope

### In Scope

| 항목 | 설명 |
|------|------|
| ILoaderTextJson 인터페이스 정의 | Devian 제공 |
| LoadFile/UnloadFile 패턴 | ExcelFileName 단위 |
| 개별 테이블 T_{TableName} | 시트 단위 |
| Dictionary 캐싱 | class 인스턴스만 |

### Out of Scope

| 항목 | 설명 |
|------|------|
| **Addressables / AssetBundle** | ❌ Unity 프로젝트 책임 |
| ILoaderTextJson 구현 | ❌ Unity 프로젝트 책임 |
| 다운로드 / 버전 / 패치 | ❌ Unity 프로젝트 책임 |
| Hot reload | ❌ 지원 안 함 |

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | `ILoaderTextJson` 인터페이스가 Devian에 정의됨 |
| 2 | Loader API가 `LoadFile/UnloadFile` 패턴을 따름 |
| 3 | Devian 코드에 **Addressables/AssetBundle 정책 없음** |
| 4 | 로딩 후 **JSON 원문 보관 안 함** |
| 5 | `UnloadFile`은 **캐시만 제거** |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `61-tablegen-implementation` | XLSX → JSON + meta 생성 |
| `65-table-loader-implementation` | 런타임 패턴 상세 |
| `67-table-loader-codegen` | Loader 코드 생성 |
| `63-build-runner` | 빌드 실행 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.9.0 | 2024-12-25 | **3줄 헤더 정책 확정**: Row 3은 순수 comment, Devian은 절대 해석하지 않음 |
| 0.8.0 | 2024-12-21 | 3행 헤더, Key=1열, type prefix, ref planned |
| 0.7.0 | 2024-12-21 | ILoaderTextJson, LoadFile/UnloadFile 패턴 확정 |
| 0.6.0 | 2024-12-21 | Multi-sheet 구조 반영 |
| 0.5.0 | 2024-12-21 | Fetch/Load 분리, 캐시 형태 확정 |
| 0.1.0 | 2024-12-20 | Initial skill definition |
