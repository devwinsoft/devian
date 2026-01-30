# 14-table-manager

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **TableManager**의 정본이다. 일반 테이블(TB_)과 스트링 테이블(ST_)의 Unity 런타임 raw data 로딩을 담당한다.

---

## 목적/범위

**TableManager**는 Unity 런타임에서 테이블 데이터를 로드하고 캐시하는 단일 진입점이다.

### 상속 (Hard Rule)

```csharp
public sealed class TableManager : AutoSingleton<TableManager>
```

AutoSingleton 기반으로 첫 `Instance` 접근 시 자동 생성된다.

### 책임

| 구분 | 책임 |
|------|------|
| 일반 테이블 | `LoadTablesAsync` - raw data 로딩 + auto-insert |
| 스트링 테이블 | `LoadStringsAsync` - raw data 로딩 + auto-insert |
| 캐시 관리 | fileName 기반 캐시, 중복 로드 방지 |
| TB 자동 insert | RegisterTbLoader 레지스트리로 자동 insert |
| ST 자동 insert | RegisterStLoader 레지스트리로 자동 insert |

### 비책임

- Addressables key 규약 강제 (프로젝트 정책)
- 빌드 시스템 (빌더가 담당)
- 데이터 포맷 정의 (ndjson/pb64 스킬이 담당)
- ST 파싱/캐시 (ST_ wrapper가 자체 관리)

---

## Addressables Key 정책 (Hard Rule)

**Addressables key는 프로젝트 정책이며, Devian/TableManager가 강제하지 않는다.**

TableManager는 전달받은 key로 TextAsset을 로드만 한다.

```csharp
// key는 개발자가 전달 - TableManager는 강제/조립 안함
yield return TableManager.Instance.LoadTablesAsync(
    "my/custom/key/Monsters",  // 프로젝트 정책에 따른 key
    TableFormat.Json,
    ...
);
```

---

## fileName 캐시 규칙 (Hard Rule)

### 캐시 기준

**캐시 키는 (format, fileName)이다. key가 아니라 fileName(TextAsset.name)이다.**

```csharp
var fileName = textAsset.name;  // 로드 성공 후
var cacheKey = new CacheKey(format, fileName);
```

### {TableName}@{Description} 파싱 규칙 (Hard Rule)

**fileName에 `@`가 있으면 앞부분이 baseName이다.**

| fileName | baseName |
|----------|----------|
| `Monsters` | `Monsters` |
| `Monsters@몬스터테이블` | `Monsters` |
| `Items@아이템목록` | `Items` |

```csharp
public static string ExtractBaseName(string fileName)
{
    var atIndex = fileName.IndexOf('@');
    return atIndex >= 0 ? fileName.Substring(0, atIndex) : fileName;
}
```

### ST 캐시 키 규칙 (Hard Rule)

**ST 로딩 캐시는 TB와 달리 fileName + language를 결합해서 캐시 키를 만든다.**

```csharp
// TB 캐시 키: (format, fileName)
var cacheKey = new CacheKey(format, fileName);

// ST 캐시 키: (format, "{fileName}:{language}")
var cacheFileName = $"{fileName}:{language}";
var cacheKey = new CacheKey(format, cacheFileName);
```

| 테이블 타입 | 캐시 키 형식 | 예시 |
|-------------|--------------|------|
| TB | `(format, fileName)` | `(Json, "Monsters")` |
| ST | `(format, "{fileName}:{language}")` | `(Json, "UIText:Korean")` |

---

## TB 자동 Insert (Registry 기반)

### RegisterTbLoader API

```csharp
public void RegisterTbLoader(string baseTableName, TbLoaderDelegate loader);

public delegate void TbLoaderDelegate(TableFormat format, string? ndjsonText, byte[]? pb64Binary);
```

### 동작 규칙 (Hard Rule)

1. `LoadTablesAsync(key, format, onError?)` 호출 → TextAsset 로드
2. `baseName = ExtractBaseName(textAsset.name)`
3. `RegisterTbLoader(baseName, ...)`가 등록되어 있지 않으면:
   - `onError` 호출 후 종료 (FAIL)
4. 등록된 loader로 TB insert 수행
5. 캐시 갱신 (이미 로드된 경우 재삽입 안함)

**onLoaded/onProgress 콜백 없음** - TableManager가 TextAsset 로드 + TB insert + 캐시 갱신까지 내부에서 완결한다.

**TB wrapper는 수동 insert를 하지 않는다** - TableManager가 Registry로 처리한다.

### DomainTableRegistry (자동 생성)

도메인 UPM 패키지에 자동 생성되는 파일:

```
Packages/com.devian.domain.{domain}/Runtime/Generated/DomainTableRegistry.g.cs
```

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Register()
{
    global::Devian.TableManager.Instance.RegisterTbLoader("Monsters", (format, text, bin) =>
    {
        if (format == TableFormat.Json && text != null)
            TB_Monsters.LoadFromNdjson(text);
        else if (format == TableFormat.Pb64 && bin != null)
            TB_Monsters.LoadFromPb64Binary(bin);
    });
}
```

### 중복 등록 금지 (Hard Rule)

같은 baseTableName이 이미 등록되어 있으면 **InvalidOperationException** 발생.

```csharp
if (mTbLoaders.ContainsKey(baseTableName))
    throw new InvalidOperationException(...);
```

### TbLoader 등록 SSOT (Hard Rule)

- TB loader (`RegisterTbLoader`) 등록의 SSOT는 **도메인 UPM의 Generated `DomainTableRegistry.g.cs`** 이다.
- 기능별 수기 Registry(예: SoundVoiceTableRegistry)는 **절대 TbLoader를 등록하지 않는다.**
  - 수기 Registry의 책임은 "델리게이트 연결/어댑터 캐시/도메인 내부 wiring"에 한정한다.
- 같은 baseTableName 중복 등록은 즉시 FAIL이다. (InvalidOperationException)

---

## API 명세

### TableFormat enum

```csharp
public enum TableFormat
{
    Json,   // ndjson
    Pb64    // pb64
}
```

### LoadTablesAsync (TB 전용)

```csharp
public IEnumerator LoadTablesAsync(
    string key,                           // Addressables key (NOT enforced)
    TableFormat format,                   // Json or Pb64
    Action<string>? onError = null)       // Error callback
```

**내부 동작 (복수 asset 로딩):**
1. **`Addressables.LoadAssetsAsync<TextAsset>(key, null)`**로 여러 TextAsset 로드
2. 성공 시 `IList<TextAsset>` 순회:
   - `baseName = ExtractBaseName(textAsset.name)` (@ 앞부분)
   - TB loader 조회 → 미등록이면 `onError` 호출 후 **해당 asset만 스킵**
   - format에 따라 데이터 준비 및 TB insert
   - 성공 시 캐시 갱신 + SharedHandle 참조 증가
3. 모든 asset이 스킵되면 핸들 Release

**에러 처리 규칙:**
- 복수 asset 중 일부 실패 시 전체 로딩을 중단하지 않음
- loader 없음 / 파싱 실패는 `onError` 호출 후 해당 asset만 스킵
- Addressables 로드 자체 실패 시에만 `yield break`

### LoadStringsAsync (ST 전용)

```csharp
public IEnumerator LoadStringsAsync(
    string key,                           // Addressables key (NOT enforced)
    TableFormat format,                   // Json or Pb64
    SystemLanguage language,
    Action<string>? onError = null)       // Error callback
```

**내부 동작 (복수 asset + language Intersection):**
1. **`Addressables.LoadAssetsAsync<TextAsset>(keys, null, MergeMode.Intersection)`**
   - `keys = new object[] { key, language.ToString() }`
   - `MergeMode.Intersection`으로 둘 다 만족하는 asset만 대상
2. 성공 시 `IList<TextAsset>` 순회:
   - `baseName = ExtractBaseName(textAsset.name)` (@ 앞부분)
   - ST loader 조회 → 미등록이면 `onError` 호출 후 **해당 asset만 스킵**
   - **언어 정책 검사:** 이미 다른 언어로 로드되어 있으면 `onError` 후 **해당 asset만 스킵** (전체 중단 금지)
   - format에 따라 데이터 준비 및 ST insert
   - 성공 시 캐시 갱신 + 언어 트래킹 갱신 + SharedHandle 참조 증가
3. 모든 asset이 스킵되면 핸들 Release

**에러 처리 규칙:**
- 복수 asset 중 일부 실패 시 전체 로딩을 중단하지 않음
- loader 없음 / 언어 충돌 / 파싱 실패는 `onError` 호출 후 해당 asset만 스킵
- Addressables 로드 자체 실패 시에만 `yield break`

### RegisterStLoader

```csharp
public void RegisterStLoader(
    string baseTableName,
    Action<TableFormat, SystemLanguage, string?, string?> loader)
```

ST loader 등록. 중복 등록 시 `InvalidOperationException`.

---

## 캐시/핸들 릴리즈 규약 (SharedHandle)

### 복수 로딩 핸들 공유 문제

`LoadAssetsAsync`는 핸들 1개가 여러 asset 결과를 포함하므로, 캐시 엔트리별 `Addressables.Release(handle)`를 그대로 하면 **중복 Release** 문제가 발생한다.

### SharedHandle (refcount) 방식

TableManager는 **SharedHandle(refcount)** 방식으로 해결한다:

```csharp
private sealed class SharedHandle
{
    public AsyncOperationHandle Handle;
    public int RefCount;
}

private sealed class CachedData
{
    public SharedHandle Shared = null!;  // Handle 직접 보관 금지
    public string? NdjsonText;
    public byte[]? Pb64Binary;
}
```

### 참조 카운트 규칙

| 상황 | 동작 |
|------|------|
| asset 로드 성공 + 캐시 추가 | `shared.RefCount++` |
| Unload 호출 | `shared.RefCount--`, RefCount==0 && IsValid면 Release |
| UnloadAll 호출 | 같은 shared가 여러 번 호출되어도 refcount로 안전하게 1번만 Release |

### ReleaseShared 유틸

```csharp
private void ReleaseShared(CachedData data)
{
    if (data.Shared == null) return;
    data.Shared.RefCount--;
    if (data.Shared.RefCount == 0 && data.Shared.Handle.IsValid())
    {
        Addressables.Release(data.Shared.Handle);
    }
}
```

### UnloadStrings

```csharp
void UnloadStrings(string baseName)
```

특정 ST의 캐시 및 언어 트래킹을 제거. 언어 변경 전 호출 필요.

### Unload

```csharp
void Unload(TableFormat format, string fileName)
void UnloadAll()
bool IsCached(TableFormat format, string fileName)
```

---

## ST Wrapper 규칙 (Hard Rule)

### 언어 고정

**ST는 Preload한 언어로 고정된다. 언어 변경은 Reload로 처리.**

```csharp
// Preload - onProgress 없음
yield return ST_UIText.PreloadAsync(key, TableFormat.Json, SystemLanguage.Korean, onError);

// Get - language 파라미터 없음
var text = ST_UIText.Get("greeting");

// 언어 변경 - ReloadAsync 사용
yield return ST_UIText.ReloadAsync(key, TableFormat.Json, SystemLanguage.English, onError);
```

### Get API

```csharp
// language 파라미터 없음 - preload된 언어 사용
public static string Get(string id)
{
    return _cache.TryGetValue(id, out var text) ? text : id;
}
```

### 내부 진입점 (DomainTableRegistry 용)

```csharp
// TableManager가 registry를 통해 호출하는 internal 메서드
internal static void _LoadFromNdjson(string text, SystemLanguage lang);
internal static void _LoadFromPb64(string pb64Text, SystemLanguage lang);
```

### 자체 파싱/캐시

ST wrapper는 자체 Dictionary 캐시와 파싱 로직을 가진다:

```csharp
private static readonly Dictionary<string, string> _cache = new();

// ndjson 파싱
private static void ParseNdjson(string content) { ... }

// pb64 파싱 (StringChunk)
private static void ParsePb64(string content) { ... }
```

---

## pb64 파싱 규칙 (Hard Rule)

### 일반 테이블 (DVGB Container)

TableManager가 base64 디코드를 수행한다:

1. `TextAsset.text`는 base64(DVGB container)
2. `Pb64Loader.LoadFromBase64(text)` → raw binary (TableManager 내부)
3. TB loader에 `(format, null, pb64Binary)` 전달

### 스트링 테이블 (StringChunk) - TableManager 디코드 안함

**TableManager는 ST pb64를 디코드하지 않는다. raw text를 그대로 전달한다.**

```csharp
// TableManager 내부 - ST pb64 처리
var text = textAsset.text;  // 멀티라인 pb64 텍스트
stLoader(format, language, null, text);  // pb64Text로 전달
```

| 테이블 타입 | TableManager 역할 | loader 파라미터 |
|-------------|-------------------|-----------------|
| TB pb64 | base64 → binary 디코드 | `(format, null, pb64Binary)` |
| ST pb64 | 디코드 안함, raw text 전달 | `(format, language, null, pb64Text)` |

ST wrapper가 자체적으로 파싱:

1. `TextAsset.text`는 멀티라인 base64 청크 (raw text)
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

## 코드젠 규칙

### TB_{TableName}.Unity.g.cs (UPM Only)

```csharp
public static partial class TB_{TableName}
{
    public static IEnumerator PreloadAsync(
        string key,
        global::Devian.TableFormat format,
        Action<string>? onError = null)
    {
        yield return global::Devian.TableManager.Instance.LoadTablesAsync(
            key, format, onError
        );
        _isLoaded = true;
    }
}
```

**onProgress 없음** - TableManager가 내부에서 완결한다.

### ST_{TableName}.g.cs (UPM Only)

```csharp
public static class ST_{TableName}
{
    public static IEnumerator PreloadAsync(
        string key,
        global::Devian.TableFormat format,
        SystemLanguage language,
        Action<string>? onError = null) { ... }

    public static string Get(string id)  // language 없음
    {
        return _cache.TryGetValue(id, out var text) ? text : id;
    }

    public static IEnumerator ReloadAsync(...) { ... }
    
    // DomainTableRegistry가 호출하는 internal 진입점
    internal static void _LoadFromNdjson(string text, SystemLanguage lang);
    internal static void _LoadFromPb64(string pb64Text, SystemLanguage lang);
}
```

**onProgress 없음** - TableManager가 내부에서 완결한다.

### DomainTableRegistry.g.cs (UPM Only)

```csharp
internal static class DomainTableRegistry
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // TB loaders
        global::Devian.TableManager.Instance.RegisterTbLoader("Monsters", (format, text, bin) =>
        {
            if (format == TableFormat.Json && text != null)
                TB_Monsters.LoadFromNdjson(text);
            else if (format == TableFormat.Pb64 && bin != null)
                TB_Monsters.LoadFromPb64Binary(bin);
        });
        
        // ST loaders
        global::Devian.TableManager.Instance.RegisterStLoader("UIText", (format, lang, text, pb64Text) =>
        {
            if (format == TableFormat.Json && text != null)
                ST_UIText._LoadFromNdjson(text, lang);
            else if (format == TableFormat.Pb64 && pb64Text != null)
                ST_UIText._LoadFromPb64(pb64Text, lang);
        });
    }
}
```

---

## 도메인 패키지 의존성 주입 규칙 (Hard Rule)

**모든 도메인 UPM 패키지는 com.devian.unity에 의존해야 한다.**

### package.json dependencies

```json
"dependencies": {
  "com.devian.core": "0.1.0",
  "com.devian.unity": "0.1.0"
}
```

### Runtime asmdef references

```json
"references": [
  "Devian.Core",
  "Devian.Unity"
]
```

### 래퍼 코드 규칙

**TableManager 호출은 반드시 `global::Devian.TableManager` 완전 수식으로 한다.**

---

## 소스 경로

| 역할 | 경로 |
|------|------|
| TableManager | `framework-cs/upm/com.devian.unity/Runtime/Table/TableManager.cs` |
| TableFormat | `framework-cs/upm/com.devian.unity/Runtime/Table/TableFormat.cs` |
| Pb64Loader | `framework-cs/upm/com.devian.core/Runtime/Core/Pb64Loader.cs` |
| TB 코드젠 | `framework-ts/tools/builder/generators/table.js` |
| UPM wrapper 생성 | `framework-ts/tools/builder/build.js` |

---

## DoD (검증 가능)

### PASS 조건

- [ ] `LoadTablesAsync(key, format, onError?)` 시그니처 (onLoaded/onProgress 없음)
- [ ] `LoadTablesAsync`가 `LoadAssetsAsync<TextAsset>(key)`로 여러 asset 처리
- [ ] `LoadStringsAsync(key, format, language, onError?)` 시그니처 (onLoaded/onProgress 없음)
- [ ] `LoadStringsAsync`가 `LoadAssetsAsync<TextAsset>([key, language], MergeMode.Intersection)`로 여러 asset 처리
- [ ] 복수 asset 중 일부 실패 시 해당 asset만 스킵 (전체 중단 금지)
- [ ] **SharedHandle(refcount)** 방식으로 중복 Release 방지
- [ ] TB loader 미등록이면 해당 asset만 스킵 + onError
- [ ] ST loader 미등록이면 해당 asset만 스킵 + onError
- [ ] ST는 baseName별 1언어 (다른 언어 로드 시 해당 asset만 스킵)
- [ ] `RegisterStLoader(string, Action<...>)` 존재
- [ ] 캐시가 `(format, fileName)` 기준
- [ ] fileName에 `@`가 있으면 baseName이 `@` 앞부분
- [ ] `DomainTableRegistry.g.cs`에 TB/ST loader 등록 코드 생성됨
- [ ] `ST_{TableName}`에 `_LoadFromNdjson/_LoadFromPb64` internal 진입점 존재
- [ ] `ST_.Get(id)` - language 파라미터 없음
- [ ] 래퍼 코드에서 `global::Devian.TableManager` 완전 수식 사용
- [ ] Unload/UnloadStrings/UnloadAll이 ReleaseShared로 안전하게 Release
- [ ] 빌드 성공

### FAIL 조건

- LoadTablesAsync에 onLoaded/onProgress 파라미터 존재
- LoadStringsAsync에 onLoaded/onProgress 파라미터 존재
- 복수 asset 중 일부 실패 시 전체 yield break
- CachedData마다 Addressables.Release(handle) 직접 호출 (중복 릴리즈)
- Addressables key 규약을 Hard로 강제하는 코드/문서
- `@` 뒤 문자열을 코드 테이블명으로 사용
- TB/ST 로딩에서 중복 등록을 조용히 덮어쓰기
- ST_.Get()에 language 파라미터 존재
- ST loader 미등록 시 조용히 무시
- 한 테이블에 다국어 동시 캐시 구현

---

## Reference

- pb64 저장: `skills/devian/35-pb64-storage/SKILL.md`
- ndjson 저장: `skills/devian/34-ndjson-storage/SKILL.md`
- String Table: `skills/devian/33-string-table/SKILL.md`
- Table Authoring: `skills/devian/30-table-authoring-rules/SKILL.md`
- TableGen: `skills/devian/42-tablegen-implementation/SKILL.md`
