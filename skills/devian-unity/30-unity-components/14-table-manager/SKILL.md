# 14-table-manager

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **TableManager**의 정본이다. 일반 테이블(TB_)과 스트링 테이블(ST_)의 Unity 런타임 로딩/캐시/언로드를 담당한다.

---

## 목적/범위

**TableManager**는 Unity 런타임에서 테이블 데이터를 로드하고 캐시하는 단일 진입점이다.

### 책임

| 구분 | 책임 |
|------|------|
| 일반 테이블 | `TB_{TableName}` 로딩 (ndjson/pb64) |
| 스트링 테이블 | `ST_{TableName}` 로딩 (ndjson/pb64) |
| 캐시 관리 | 로드된 테이블 캐시, 중복 로드 방지 |
| 언로드 | 개별/전체 언로드 |

### 비책임

- Addressables 그룹/프로파일 설정
- 빌드 시스템 (빌더가 담당)
- 데이터 포맷 정의 (ndjson/pb64 스킬이 담당)

---

## Addressables Key/Label 규약 (Hard Rule)

### 일반 테이블

```
table/{format}/{TableName}
```

| 예시 | Address/Label |
|------|---------------|
| TestSheet (ndjson) | `table/ndjson/TestSheet` |
| TestSheet (pb64) | `table/pb64/TestSheet` |

### 스트링 테이블

```
string/{format}/{Language}/{TableName}
```

| 예시 | Address/Label |
|------|---------------|
| UIText (Korean, ndjson) | `string/ndjson/Korean/UIText` |
| UIText (English, pb64) | `string/pb64/English/UIText` |

### Label = Key 규칙

**Address와 Label은 반드시 동일 문자열이어야 한다.**

> **이유**: DownloadManager가 label 기반으로 동작하므로, label = key로 통일해야 개별 다운로드 가능.

---

## 캐시 키 규칙 (Hard Rule)

### 일반 테이블

```
(format, tableName)
```

- 같은 테이블을 ndjson/pb64 둘 다 로드 가능 (별도 캐시)

### 스트링 테이블

```
(format, language, tableName)
```

- 같은 테이블을 언어별로 로드 가능 (별도 캐시)

---

## API 명세

### 일반 테이블

```csharp
// 프리로드 (DownloadManager 다운로드 + Addressables 로드)
IEnumerator PreloadTableAsync(
    string format,
    string tableName,
    Action<string?, byte[]?>? onLoaded = null,
    Action<float>? onProgress = null,
    Action<string>? onError = null)

// 로드 상태 확인
bool IsTableLoaded(string format, string tableName)

// 언로드
void UnloadTable(string format, string tableName)

// Raw 데이터 접근
string? GetTableText(string tableName)    // ndjson
byte[]? GetTableBinary(string tableName)  // pb64
```

### 스트링 테이블

```csharp
// 프리로드
IEnumerator PreloadStringAsync(
    string format,
    SystemLanguage language,
    string tableName,
    Action<float>? onProgress = null,
    Action<string>? onError = null)

// 텍스트 조회 (fallback: language → English → id)
string GetString(string format, SystemLanguage language, string tableName, string id)

// 로드 상태 확인
bool IsStringLoaded(string format, SystemLanguage language, string tableName)

// 언로드
void UnloadString(string format, SystemLanguage language, string tableName)
```

### 전체 언로드

```csharp
void UnloadAll()
```

---

## pb64 파싱 규칙

### 일반 테이블 (DVGB Container)

1. `TextAsset.text`는 base64(DVGB container)
2. `Pb64Loader.LoadFromBase64(text)` → raw binary
3. `Pb64Loader.ParseRows(rawBinary, onJsonRow)` → JSON row 문자열 반복

```csharp
// 코드젠된 TB_ 호출 예시
TB_TestSheet.LoadFromPb64Binary(rawBinary);
```

### 스트링 테이블 (StringChunk)

1. `TextAsset.text`는 멀티라인 base64 청크
2. 줄 단위로 `Split('\n')`
3. 각 줄을 base64 decode → protobuf StringChunk parse

**StringChunk 포맷 (변경 금지):**

```protobuf
message StringEntry {
    string id = 1;
    string text = 2;
}

message StringChunk {
    repeated StringEntry entries = 1;
}
```

---

## Language Fallback 규칙

```
1. 요청 language 테이블에서 id 검색
2. 없거나 빈 문자열이면 → English 테이블에서 검색
3. 그래도 없으면 → id 자체를 반환
```

**SystemLanguage.Unknown은 English로 치환:**

```csharp
if (language == SystemLanguage.Unknown)
    language = SystemLanguage.English;
```

---

## 코드젠 규칙 (빌더 연동)

### TB_{TableName} 클래스

1. **partial 선언**: `public static partial class TB_{TableName}`
2. **LoadFromPb64Binary 메서드**: Unity 비의존, 모듈에서도 컴파일 가능

```csharp
// {DomainName}.g.cs (module + UPM 공통)
public static partial class TB_{TableName}
{
    public static void LoadFromNdjson(string ndjson) { ... }
    public static void LoadFromPb64Binary(byte[] rawBinary) { ... }
}
```

### TB_{TableName}.Unity.g.cs (UPM Only)

Unity 전용 wrapper. 모듈에는 생성되지 않음.

```csharp
// TB_{TableName}.Unity.g.cs (UPM only)
public static partial class TB_{TableName}
{
    public static IEnumerator PreloadAsync(
        string format,
        Action<float>? onProgress = null,
        Action<string>? onError = null)
    {
        yield return global::Devian.TableManager.Instance.PreloadTableAsync(
            format,
            "{TableName}",
            (rawText, rawBinary) => {
                if (format == "ndjson" && rawText != null)
                    LoadFromNdjson(rawText);
                else if (format == "pb64" && rawBinary != null)
                    LoadFromPb64Binary(rawBinary);
            },
            onProgress,
            onError
        );
    }

    public static void Unload(string format) { ... }
    public static bool IsLoaded(string format) { ... }
}
```

### ST_{TableName}.g.cs (UPM Only)

스트링 테이블 wrapper. 모듈에는 생성되지 않음.

```csharp
// ST_{TableName}.g.cs (UPM only)
public static class ST_{TableName}
{
    public static IEnumerator PreloadAsync(
        string format,
        SystemLanguage language,
        Action<float>? onProgress = null,
        Action<string>? onError = null)
    {
        yield return global::Devian.TableManager.Instance.PreloadStringAsync(
            format, language, "{TableName}", onProgress, onError
        );
    }

    public static string Get(string format, SystemLanguage language, string id)
    {
        return global::Devian.TableManager.Instance.GetString(format, language, "{TableName}", id);
    }

    public static void Unload(string format, SystemLanguage language) { ... }
    public static bool IsLoaded(string format, SystemLanguage language) { ... }
}
```

---

## 중복 로드 정책 (Hard Rule)

**이미 로드된 테이블을 다시 로드 요청하면 즉시 완료 처리한다.**

```csharp
if (mTables.ContainsKey(cacheKey))
{
    onProgress?.Invoke(1f);
    yield break;  // 즉시 완료
}
```

- 재로드가 필요하면 먼저 `Unload()` 호출 후 다시 로드

---

## 도메인 패키지 의존성 주입 규칙 (Hard Rule)

**ST_*.g.cs 및 TB_*.Unity.g.cs는 TableManager를 사용하므로, 모든 도메인 UPM 패키지는 com.devian.unity에 의존해야 한다.**

### package.json dependencies (필수)

```json
"dependencies": {
  "com.devian.core": "0.1.0",
  "com.devian.unity": "0.1.0"
}
```

### Runtime asmdef references (필수)

```json
"references": [
  "Devian.Core",
  "Devian.Unity"
]
```

### 최소 보장 대상

| 패키지 | 의존성 |
|--------|--------|
| com.devian.domain.common | com.devian.unity 필수 |
| com.devian.domain.{template} | com.devian.unity 필수 |
| com.devian.domain.* (모든 도메인) | com.devian.unity 필수 |

### 래퍼 코드 규칙 (필수)

**TableManager 호출은 반드시 `global::Devian.TableManager` 완전 수식으로 한다.**

```csharp
// CORRECT
yield return global::Devian.TableManager.Instance.PreloadTableAsync(...);

// WRONG - namespace 충돌/누락 가능
yield return TableManager.Instance.PreloadTableAsync(...);
```

---

## 소스 경로

| 역할 | 경로 |
|------|------|
| TableManager | `framework-cs/upm/com.devian.unity/Runtime/Table/TableManager.cs` |
| Pb64Loader | `framework-cs/upm/com.devian.core/Runtime/Core/Pb64Loader.cs` |
| TB 코드젠 | `framework-ts/tools/builder/generators/table.js` |
| UPM wrapper 생성 | `framework-ts/tools/builder/build.js` |

---

## DoD (검증 가능)

### PASS 조건

- [ ] TableManager가 UPM + UnityExample에 존재 (동일 내용)
- [ ] TB_ 클래스가 `static partial`로 생성됨
- [ ] TB_.LoadFromPb64Binary(byte[]) 메서드 존재 (Unity 비의존)
- [ ] TB_{TableName}.Unity.g.cs가 UPM Runtime/Generated에 생성됨
- [ ] ST_{TableName}.g.cs가 UPM Runtime/Generated에 생성됨
- [ ] 래퍼 코드에서 `global::Devian.TableManager.Instance` 완전 수식 사용
- [ ] 도메인 package.json에 `com.devian.unity` dependency 존재
- [ ] 도메인 asmdef에 `Devian.Unity` reference 존재
- [ ] 모듈에는 Unity wrapper 파일이 없음
- [ ] Addressables key 규약이 문서와 일치
- [ ] 빌드 성공

### FAIL 조건

- StringTableManager 참조가 남아있음
- TB_ 생성물에 Unity 의존성이 포함됨
- 도메인 패키지에서 com.devian.unity 의존성 누락
- 도메인 asmdef에서 Devian.Unity reference 누락
- 래퍼 코드에서 TableManager를 완전 수식 없이 호출
- format이 ndjson/pb64 외의 값
- StringChunk/DVGB 포맷 변경

---

## Reference

- pb64 저장: `skills/devian/35-pb64-storage/SKILL.md`
- ndjson 저장: `skills/devian/34-ndjson-storage/SKILL.md`
- String Table: `skills/devian/33-string-table/SKILL.md`
- DownloadManager: `skills/devian-unity/30-unity-components/12-download-manager/SKILL.md`
- AssetManager: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md`
