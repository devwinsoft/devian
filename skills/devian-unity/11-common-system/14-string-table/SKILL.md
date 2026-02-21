# 14-string-table

Status: ACTIVE
AppliesTo: v10

## SSOT

이 문서는 **String Table** 생성 및 런타임 규약의 정본이다.

---

## 목적/범위

**Common 도메인 기준 String Table(TEXT) 규약 및 런타임 소비 규약을 고정한다.**

다국어 텍스트 테이블을 ndjson/pb64로 내보내고, DownloadManager(Addressables Label) + 런타임 Get까지 규약을 고정한다.

- **입력(마스터)**: XLSX 시트, 컬럼: `id`, `description`, `{Language}`, ...
- **출력**: 언어별 ndjson/pb64 파일
- **런타임**: DownloadManager 다운로드 → TableManager 로드/캐시/조회 → ST_{TableName} wrapper

저장 규약 참조:
- **NDJSON 저장**: `skills/devian-tools/11-builder/34-ndjson-storage/SKILL.md`
- **pb64 저장**: `skills/devian-tools/11-builder/35-pb64-storage/SKILL.md`

---

## Domain 입력 설정 (Hard Rules)

### 설정 필드

| 필드 | 타입 | 설명 |
|------|------|------|
| `domains.{Domain}.stringDir` | string | String Table XLSX 디렉토리 |
| `domains.{Domain}.stringFiles` | string[] | glob 패턴 배열 (예: `["*.xlsx"]`) |

### input.json 예시

```json
{
  "domains": {
    "Common": {
      "tableDir": "Domains/Common",
      "tableFiles": ["CommonTable.xlsx"],
      "stringDir": "Domains/Common",
      "stringFiles": ["LocalizedText.xlsx"]
    }
  }
}
```

### Hard Rule: stringDir/stringFiles 전용 입력

**String Table 생성기는 오직 `stringDir/stringFiles`만 입력으로 사용한다.**

- `tableDir/tableFiles`에서 String Table을 찾거나 처리하는 fallback은 **존재하지 않는다**.
- `stringDir`와 `stringFiles`는 반드시 함께 설정해야 한다 (하나만 있으면 **빌드 FAIL**).
- 설정되지 않으면 String Table 처리를 Skip한다.

### Hard Rule: 입력 파일 겹침 금지

**`stringDir/stringFiles`로 매칭된 파일이 `tableDir/tableFiles`로도 매칭되면 빌드 FAIL**

```
[FAIL] Input file overlap detected in domain 'Game'.
  The following files are matched by both tableFiles and stringFiles:
  SomeTable.xlsx
  This is forbidden to prevent silent processing errors.
  Move string table XLSX files to a separate directory (stringDir).
```

- 이유: table 파이프라인과 string 파이프라인이 같은 파일을 동시에 처리하면 침묵 오류(silent error) 발생
- 해결: String Table XLSX는 반드시 별도 디렉토리(`stringDir`)에 배치

---

## 입력 규칙 (XLSX 마스터)

### Canonical Master (Hard Rule)

**String Table(TEXT)은 오직 Common 도메인에만 존재한다.**

- `LocalizedText.xlsx`: `input/Domains/Common/LocalizedText.xlsx`
- 이 파일은 `domains.Common.stringDir/stringFiles`로만 매칭되어야 하며, `tableDir/tableFiles`와 overlap 되면 빌드 FAIL.

**금지:** Game/Sound 등 다른 도메인에 동일 역할의 LocalizedText.xlsx를 두지 않는다 (중복은 FAIL).

### 헤더 컬럼

| 컬럼명 | 필수 | 설명 |
|--------|------|------|
| `id` | ✓ | 텍스트 식별자 (string, PK) |
| `description` | ✓ | 설명/메모 (출력에 **포함 안 됨**) |
| `{Language}` | 1개 이상 | 언어별 텍스트 |

### Language 컬럼명 규칙 (Hard Rule)

**언어 컬럼명은 반드시 `UnityEngine.SystemLanguage` enum name과 정확히 동일해야 한다.**

```
// 허용되는 언어 컬럼명 예시
English, Korean, Japanese, ChineseSimplified, ChineseTraditional, French, German, Spanish, Portuguese, Russian, Italian, ...
```

- 컬럼명이 SystemLanguage enum에 없으면 **빌드 FAIL**
- 대소문자 정확히 일치 필요

### 검증 규칙 (빌드 FAIL 조건)

- `id` 값이 빈 문자열이면 FAIL
- `id` 중복이면 FAIL
- 언어 컬럼명이 SystemLanguage enum과 불일치하면 FAIL

---

## 출력 경로 규칙

### Staging (빌드 중간 산출물)

```
{tempDir}/{DomainKey}/data/string/{format}/{Language}/{TableName}.{ext}
```

### Final (최종 배포)

```
{stringDir}/{format}/{Language}/{TableName}.{ext}
```

| 변수 | 설명 |
|------|------|
| `{format}` | `ndjson` 또는 `pb64` |
| `{Language}` | SystemLanguage enum name (예: `English`, `Korean`) |
| `{TableName}` | XLSX SheetName |
| `{ext}` | ndjson → `.json`, pb64 → `.asset` |

**도메인 폴더 미사용 (Hard Rule):**
- 최종 경로에 `{DomainKey}` 폴더를 생성하지 않는다.
- 동일 파일명 충돌 시 빌드 FAIL.

### 예시

**Staging:**
```
temp/Game/data/string/ndjson/English/UIText.json
temp/Game/data/string/pb64/English/UIText.asset
```

**Final:**
```
{stringDir}/ndjson/English/UIText.json
{stringDir}/pb64/English/UIText.asset
```

---

## Addressables Key/Label 규약 (DownloadManager 연동 핵심)

### Address (Key)

```
string/{format}/{Language}/{TableName}
```

### Label

**반드시 Address와 동일 문자열을 Label로 붙인다.**

```
// 예시
Address: string/ndjson/Korean/UIText
Label:   string/ndjson/Korean/UIText
```

> **이유**: DownloadManager가 label 기반으로 동작하므로, label = key로 통일해야 `overrideLabels`로 개별 다운로드 가능.

---

## ndjson 포맷 규칙

**NDJSON 저장 규약은 `skills/devian-tools/11-builder/34-ndjson-storage/SKILL.md`를 따른다.**

### String Table ndjson 필드 구조

**한 줄 = 하나의 JSON 객체 (description 없음)**

```json
{"id":"greeting","text":"Hello"}
{"id":"farewell","text":"Goodbye"}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string | 텍스트 식별자 |
| `text` | string | 해당 언어 텍스트 (빈 문자열 허용) |

---

## pb64 포맷 규칙 (청크 기반)

**pb64 저장(확장자/포장)은 `skills/devian-tools/11-builder/35-pb64-storage/SKILL.md`를 따르며, Unity 소비를 위해 `.asset`로 저장한다.**

### 페이로드 구조

**페이로드는 여러 줄, 한 줄 = base64(StringChunk bytes)**

```
base64(StringChunk1)
base64(StringChunk2)
...
```

### Protobuf 메시지 정의 (wire 포맷)

```protobuf
message StringEntry {
    string id = 1;
    string text = 2;
}

message StringChunk {
    repeated StringEntry entries = 1;
}
```

### 청크 Flush 정책 (고정값)

| 파라미터 | 값 | 설명 |
|----------|-----|------|
| `targetChunkBytes` | 65536 | 청크 바이트 목표치 |
| `maxEntriesPerChunk` | 256 | 청크당 최대 엔트리 수 |

- "bytes ≥ target" 또는 "entries ≥ max"이면 flush

---

## pb64 Encoding/Decoding 규칙 (Hard Rule)

### Encoding (Generator)

1. `StringChunk`(protobuf bytes)를 base64로 인코딩
2. 청크 여러 개면 여러 라인으로 저장 (canonical: `\n` 구분)
3. 저장(포장)은 `35-pb64-storage` 규약에 따라 `.asset` YAML로 감싼다 (block scalar `m_Script: |`)

### Decoding (Runtime) — Hard Rule

런타임에서 `TextAsset.text`는 **YAML 전체일 수 있다** (헤더/필드 라인 포함).

디코더는 아래를 지원해야 한다:

| 케이스 | 처리 방법 |
|--------|-----------|
| YAML block scalar marker 라인 (예: `m_Script: |`) | 무시 (base64 아님) |
| 기타 YAML 라인 (헤더, 필드) | 무시 (base64 아님) |
| base64 청크가 개행으로 나뉜 canonical 케이스 | `\n` 구분 |
| (방어) base64 청크가 `|`로 이어진 케이스 | `|` 구분자 지원 |

### 로그 정책 (Hard Rule)

- 디코드/파싱 실패 시 `Devian.Log.Error(...)` 로 기록
- **단, YAML 같은 "명백히 base64가 아닌 라인"은 사전 필터링으로 디코드 시도 자체를 하지 않음**
- 이를 통해 로그 스팸을 방지

### base64 후보 판별 (IsLikelyBase64)

```csharp
// base64 문자: A-Za-z0-9+/= 만 허용
// 길이 4 미만이면 false
private static bool IsLikelyBase64(string s)
{
    if (string.IsNullOrEmpty(s) || s.Length < 4) return false;
    foreach (char c in s)
    {
        bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                  (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=';
        if (!ok) return false;
    }
    return true;
}
```

---

## 런타임 규칙

### Language 고정 규칙 (Hard Rule)

**ST는 Preload한 언어로 고정된다. 언어 변경은 Reload로 처리.**

```csharp
// Preload - 언어 지정 (onProgress 없음)
yield return ST_UIText.PreloadAsync(key, TableFormat.Json, SystemLanguage.Korean, onError);

// Get - language 파라미터 없음 (preload된 언어 사용)
var text = ST_UIText.Get("greeting");

// 언어 변경 - Reload 필요 (TableManager.UnloadStrings + PreloadAsync)
yield return ST_UIText.ReloadAsync(key, TableFormat.Json, SystemLanguage.English, onError);
```

### TableManager LoadStringsAsync (Hard Rule)

**LoadStringsAsync는 onLoaded/onProgress 콜백 없이 내부에서 완결한다.**

```csharp
public IEnumerator LoadStringsAsync(
    string key,
    TableFormat format,
    SystemLanguage language,
    Action<string>? onError = null)
```

- TextAsset 로드 → baseName 파싱 → ST loader registry로 insert
- 이미 다른 언어로 로드되어 있으면 FAIL (언어 변경은 Reload 필요)

### Language 기본값 규칙

**load 호출에서 language가 `Unknown`이면 `English`로 치환**

```csharp
if (lang == SystemLanguage.Unknown)
    lang = SystemLanguage.English;
```

### ST_ 캐시 규칙 (Hard Rule)

**ST_ wrapper는 자체 Dictionary 캐시를 관리한다.**

```csharp
private static readonly Dictionary<string, string> _cache = new();
private static SystemLanguage _loadedLanguage;
```

### Get Fallback 규칙

```
1. 자체 캐시에서 id 검색
2. 없으면 → id 자체를 반환 (fallback)
```

**참고:** 언어간 fallback(Korean → English)은 지원하지 않는다. 언어 변경이 필요하면 Reload를 사용.

---

## AssetManager 캐시 금지 (Hard Rule)

**String Table은 AssetManager의 name 캐시를 사용하면 안 된다.**

- 이유: 언어별로 같은 TableName 파일이 존재하므로 name 충돌 발생
- 해결: TableManager가 `(format, language, tableName)` 키로 별도 캐시

---

## 소스 경로

| 역할 | 경로 |
|------|------|
| 생성기 | `framework-ts/tools/builder/generators/string-table.js` |
| 런타임(UPM) | `framework-cs/upm/com.devian.foundation/Runtime/Unity/Table/TableManager.cs` |
| 런타임(Example) | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Unity/Table/TableManager.cs` |
| ST_ wrapper | UPM 도메인 패키지 `Runtime/Generated/ST_{TableName}.g.cs` |

---

## DoD (검증 가능)

### PASS 조건

- [ ] `input.json`에 `stringDir/stringFiles`가 설정됨
- [ ] String Table 처리가 `stringDir/stringFiles`만 사용
- [ ] 언어 컬럼명이 SystemLanguage enum과 일치
- [ ] staging에 `string/ndjson/{Language}/{Table}.json` 생성됨
- [ ] staging에 `string/pb64/{Language}/{Table}.asset` 생성됨 (청크/여러 줄)
- [ ] ndjson 파일에 description 필드 없음
- [ ] pb64 파일이 Unity TextAsset YAML 형식이며 block scalar(`|`) 사용
- [ ] 런타임에서 language `Unknown`이면 English 기본 적용
- [ ] ST_.Get(id) - language 파라미터 없음
- [ ] ST_.PreloadAsync(key, format, language, onError?) - onLoaded/onProgress 없음
- [ ] ST_.ReloadAsync로 언어 변경 가능
- [ ] LoadStringsAsync(key, format, language, onError?) - onLoaded/onProgress 없음

### FAIL 조건

- `stringDir`만 있거나 `stringFiles`만 있음 (incomplete config)
- `stringDir/stringFiles`와 `tableDir/tableFiles` 파일 겹침
- `tableDir/tableFiles`에서 String Table 처리 시도
- 언어 컬럼명이 SystemLanguage enum과 불일치
- id 빈 값 또는 중복
- description이 출력에 포함됨
- pb64 확장자가 `.pb64` (`.asset`이어야 함)
- AssetManager name 캐시 직접 사용
- LoadStringsAsync에 onLoaded/onProgress 파라미터 존재
- ST loader 미등록 시 조용히 무시

---

## String Table ID Types (Inspector binding)

### Hard Rules

1. **타입 생성**: 각 StringTable `{TableName}`마다 C# `{TableName}_ID(string)` 타입을 생성한다.
2. **이름 충돌 금지**: 같은 도메인에서 TB 테이블명과 ST 테이블명이 동일하면 **빌드 FAIL** (타입 충돌 방지).
3. **Unity Editor 생성**: `{TableName}_ID.Editor.cs`를 자동 생성한다.
4. **확장자 필터**: `.json`만 로드 (`.ndjson` 필터 사용 시 **FAIL**).
5. **NDJSON 파싱**: `{"id","text"}` 중 `id`만 추출하여 Selector 목록 구성.
6. **클릭 즉시 적용**: SelectionGrid 항목 클릭 시 Value 적용 + 창 Close, Apply 버튼 **금지**.

### 생성물 구조

```csharp
// {TableName}_ID 타입 (Runtime)
[Serializable]
public sealed class {TableName}_ID
{
    public string Value = string.Empty;

    public static implicit operator string({TableName}_ID id) => id.Value;
    public static implicit operator {TableName}_ID(string value) => new {TableName}_ID { Value = value };
}

// IsValid extension
public static bool IsValid(this {TableName}_ID? obj) => obj != null && !string.IsNullOrEmpty(obj.Value);
```

### Editor 생성물 구조

```csharp
// {TableName}_ID.Editor.cs (Editor/Generated)
public sealed class {TableName}IdSelector : BaseEditorID_Selector
{
    protected override string GetDisplayTypeName() => "{TableName}_ID";

    public override void Reload()
    {
        ClearItems();
        // AssetManager.FindAssets<TextAsset>("{TableName}")
        // 1) .json 확장자만 필터
        // 2) assetPath 정렬 후 첫 번째 1개 선택
        // 3) NDJSON 파싱: JsonUtility.FromJson<StringEntry>(line)
        // 4) AddItem(entry.id)
    }

    [Serializable]
    private class StringEntry { public string id = string.Empty; public string text = string.Empty; }
}

[CustomPropertyDrawer(typeof({TableName}_ID))]
public sealed class {TableName}_ID_Drawer : BaseEditorID_Drawer<{TableName}IdSelector>
{
    protected override {TableName}IdSelector GetSelector()
    {
        var w = ScriptableObject.CreateInstance<{TableName}IdSelector>();
        w.ShowUtility();
        return w;
    }
}
```

---

## Reference

- Parent: `skills/devian-unity/11-common-system/00-overview/SKILL.md`
- NDJSON 저장: `skills/devian-tools/11-builder/34-ndjson-storage/SKILL.md`
- pb64 저장: `skills/devian-tools/11-builder/35-pb64-storage/SKILL.md`
- Related: `skills/devian-unity/10-foundation/19-download-manager/SKILL.md` (다운로드 연동)
- Related: `skills/devian-unity/10-foundation/18-asset-manager/SKILL.md` (캐시 금지 규칙)
- Related: `skills/devian-tools/11-builder/32-json-row-io/SKILL.md` (일반 테이블 포맷)
