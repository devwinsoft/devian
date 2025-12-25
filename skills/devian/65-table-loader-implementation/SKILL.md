# Devian – 65 Table Loader Implementation

## Purpose

**Unity 런타임에서 Devian 테이블을 로드/캐시/조회/언로드하는 표준 사용법을 문서화한다.**

Devian은 fetch(Addressables/AssetBundle/Resources)를 다루지 않으며, Unity 프로젝트 코드가 `ILoader*` 구현체로 JSON text를 공급한다.

---

## Belongs To

**Consumer / Runtime**

---

## 공통 핵심 원칙 (최우선)

| # | 원칙 |
|---|------|
| 1 | **Devian은 비즈니스 로직을 다루지 않는다** |
| 2 | **sub-domain / language / region / shard 개념을 해석하지 않는다** |
| 3 | **{table}@{sub} 같은 키의 의미는 개발자 정책이다** |
| 4 | **테이블 종류 1개당 컨테이너는 정확히 1개** |
| 5 | **분할 데이터는 "여러 번 Load"로 합쳐진다** |

---

## LoadMode 정책 (핵심)

### LoadMode enum

```csharp
public enum LoadMode
{
    Merge,      // 기본값
    Replace
}
```

### 동작 규칙

| Mode | 동작 |
|------|------|
| **Merge** (기본) | 기존 캐시 유지 + 새 데이터 병합. **key 충돌 시 overwrite** |
| **Replace** | 기존 캐시 Clear 후 로드 |

### API

```csharp
void LoadFromJson(string json, LoadMode mode = LoadMode.Merge);
```

- **명시하지 않으면 Merge로 동작**

---

## Hard Rules (MUST)

### Hard Rule 1: Fetch 정책 분리

| # | Rule |
|---|------|
| 1-1 | **Devian은 Addressables/AssetBundle/Resources 정책을 절대 구현/강제하지 않는다** |
| 1-2 | **Devian은 loader interface를 통해 제공받은 결과(string json)만 처리한다** |

### Hard Rule 2: 캐싱 규칙

| # | Rule |
|---|------|
| 2-1 | **Row class 인스턴스만 캐시** (`Dictionary<TKey, Row>`) |
| 2-2 | 원본 json string / TextAsset / bytes 핸들 미보관 |

### Hard Rule 3: 언로드 규칙

| # | Rule |
|---|------|
| 3-1 | `Unload()`는 **캐시된 Row 인스턴스만 제거** |
| 3-2 | **에셋 해제 / 번들 언로드는 Devian 책임 아님** |

### Hard Rule 4: sub-domain 정책

| # | Rule |
|---|------|
| 4-1 | **Devian은 sub-domain / language 정책을 제공하지 않는다** |
| 4-2 | **분할 전략은 개발자/게임 로직 책임** |

---

## Loader Interface

### 네이밍 규칙

```
ILoader{AssetType}{FormatOptional}
```

### 현재 필수 인터페이스

```csharp
namespace Devian.Tables
{
    public interface ILoaderTextJson
    {
        string Load(string key);
    }
}
```

---

## 표준 사용 API

### Table 컨테이너 API

| API | 설명 |
|-----|------|
| `IsLoaded` | 로드 여부 |
| `LoadFromJson(string json, LoadMode mode = LoadMode.Merge)` | JSON 로드 |
| `Get(key)` | 키로 조회 (없으면 예외) |
| `TryGet(key, out row)` | 키로 조회 (없으면 false) |
| `Unload()` | 캐시 제거 |

### ExcelFile 단위 API

```csharp
Table.LoadFile_{ExcelFileName}(ILoaderTextJson loader);
Table.UnloadFile_{ExcelFileName}();
```

---

## 표준 사용 예제

### 1. 분할 데이터 로딩 (의도된 사용법)

```csharp
var loader = new MyJsonLoader();

// 분할된 데이터 로드 (Merge)
Table.T_Items.LoadFromJson(loader.Load("items@base"));
Table.T_Items.LoadFromJson(loader.Load("items@kr"));
Table.T_Items.LoadFromJson(loader.Load("items@jp"));

// key 충돌 시: jp가 kr을, kr이 base를 덮어씀

// 조회
var item = Table.T_Items.Get(1004);
```

### 2. 완전 교체 로딩

```csharp
// 기존 캐시 Clear 후 새 데이터로 교체
Table.T_Items.LoadFromJson(loader.Load("items_full"), LoadMode.Replace);
```

### 3. 언로드

```csharp
Table.T_Items.Unload();
```

### 4. ExcelFile 단위 로딩 (편의 API)

```csharp
var loader = new BundleJsonLoader("Data/common");

// ExcelFile 단위 로드 (내부에서 Merge 사용)
Table.LoadFile_GameData(loader);

// 조회
var item = Table.T_Items.Get(1001);

// 언로드
Table.UnloadFile_GameData();
```

### 5. Unity 프로젝트 측 Loader 구현

```csharp
// Unity 프로젝트 코드 (Devian 밖)
public sealed class BundleJsonLoader : Devian.Tables.ILoaderTextJson
{
    private readonly string _basePath;
    
    public BundleJsonLoader(string basePath)
    {
        _basePath = basePath;
    }
    
    public string Load(string key)
    {
        // key = "items", "items@kr" 등
        // Devian은 key의 의미를 해석하지 않음
        var path = $"{_basePath}/{key}";
        return BundleManager.LoadText(path);
    }
}
```

---

## 명시적 금지

| # | 금지 |
|---|------|
| 1 | **Devian은 sub-domain / language 정책을 제공하지 않는다** |
| 2 | **분할 전략은 개발자/게임 로직 책임** |
| 3 | **{table}@{sub} 형식의 key를 Devian이 해석하지 않는다** |

---

## ref: 정책

```
ref:*는 지원 예정 (Planned)
현재 사용 불가
tablegen 단계에서 빌드 실패
```

---

## 책임 분리

| 책임 | Devian | Unity 프로젝트 |
|------|--------|---------------|
| ILoaderTextJson 인터페이스 정의 | ✅ | |
| ILoaderTextJson 구현 | | ✅ |
| JSON → Row 파싱 | ✅ | |
| Row 인스턴스 캐싱 | ✅ | |
| LoadMode 처리 (Merge/Replace) | ✅ | |
| 분할 전략 결정 | | ✅ |
| 에셋 로드/해제 | | ✅ |
| 번들 버전 관리 | | ✅ |

---

## 최종 한 줄 요약

> **Devian Table은 하나의 컨테이너에 여러 JSON을 Merge 로드할 수 있도록 설계되며,**
> **분할 의미(sub-domain, language)는 개발자가 결정하고 Devian은 해석하지 않는다.**

---

## Acceptance Criteria

| # | 조건 |
|---|------|
| 1 | Fetch 정책 분리가 Hard Rule로 명시됨 |
| 2 | **LoadMode 정책 (Merge/Replace) 명시됨** |
| 3 | **분할 데이터 로딩 예제 포함됨** |
| 4 | **sub-domain 해석 금지 명시됨** |
| 5 | ref:*는 "지원 예정"으로 명시됨 |
| 6 | 캐시/언로드 책임 구분됨 |

---

## Related Skills

| Skill | 관계 |
|-------|------|
| `61-tablegen-implementation` | XLSX → JSON + meta 생성 |
| `67-table-loader-codegen` | Loader 코드 생성 |
| `24-table-authoring-rules` | 테이블 작성 규칙 |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.7.0 | 2024-12-25 | **LoadMode 정책 추가**, 분할 데이터 예제, sub-domain 해석 금지 |
| 0.6.0 | 2024-12-25 | 작업 지시서 반영, 예제 강화 |
| 0.5.0 | 2024-12-25 | primaryKeyFieldName 사용 명시 |
| 0.1.0 | 2024-12-21 | Initial skill definition |
