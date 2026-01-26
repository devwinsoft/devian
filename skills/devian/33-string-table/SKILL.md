# 33-string-table

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 **String Table** 생성 및 런타임 규약의 정본이다.

---

## 목적/범위

**다국어 텍스트 테이블을 ndjson/pb64로 내보내고, DownloadManager(Addressables Label) + 런타임 Get까지 규약을 고정한다.**

- **입력(마스터)**: XLSX 시트, 컬럼: `id`, `description`, `{Language}`, ...
- **출력**: 언어별 ndjson/pb64 파일
- **런타임**: DownloadManager 다운로드 → StringTableManager 로드/캐시/조회

저장 규약 참조:
- **NDJSON 저장**: `skills/devian/34-ndjson-storage/SKILL.md`
- **pb64 저장**: `skills/devian/35-pb64-storage/SKILL.md`

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
    "Game": {
      "tableDir": "Game/tables",
      "tableFiles": ["*.xlsx"],
      "stringDir": "Game/strings",
      "stringFiles": ["*.xlsx"]
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
{bundleDir}/Strings/{format}/{Language}/{TableName}.{ext}
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
{bundleDir}/Strings/ndjson/English/UIText.json
{bundleDir}/Strings/pb64/English/UIText.asset
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

**NDJSON 저장 규약은 `skills/devian/34-ndjson-storage/SKILL.md`를 따른다.**

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

**pb64 저장(확장자/포장)은 `skills/devian/35-pb64-storage/SKILL.md`를 따르며, Unity 소비를 위해 `.asset`로 저장한다.**

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

## 런타임 규칙

### Language 기본값 규칙

**load/get 호출에서 language가 없거나 `Unknown`이면 `English`로 치환**

```csharp
if (lang == SystemLanguage.Unknown)
    lang = SystemLanguage.English;
```

### 캐시 키 규칙 (Hard Rule)

**캐시는 반드시 `(format, language, tableName)` 단위로 관리**

```csharp
// 올바른 캐시 키
var cacheKey = $"{format}/{language}/{tableName}";

// 금지: tableName만으로 캐시
var cacheKey = tableName; // FAIL - 언어 충돌 발생
```

### Get Fallback 규칙

```
1. 현재 language 테이블에서 id 검색
2. 없거나 빈 문자열이면 → English 테이블에서 검색
3. 그래도 없으면 → id 자체를 반환
```

---

## AssetManager 캐시 금지 (Hard Rule)

**String Table은 AssetManager의 name 캐시를 사용하면 안 된다.**

- 이유: 언어별로 같은 TableName 파일이 존재하므로 name 충돌 발생
- 해결: StringTableManager가 `(format, language, tableName)` 키로 별도 캐시

---

## 소스 경로

| 역할 | 경로 |
|------|------|
| 생성기 | `framework-ts/tools/builder/generators/string-table.js` |
| 런타임(UPM) | `framework-cs/upm/com.devian.unity/Runtime/StringTable/StringTableManager.cs` |
| 런타임(Example) | `framework-cs/apps/UnityExample/Packages/com.devian.unity/Runtime/StringTable/StringTableManager.cs` |

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
- [ ] 런타임에서 language 미지정 시 English 기본 적용
- [ ] Get fallback이 English → id 순서로 동작

### FAIL 조건

- `stringDir`만 있거나 `stringFiles`만 있음 (incomplete config)
- `stringDir/stringFiles`와 `tableDir/tableFiles` 파일 겹침
- `tableDir/tableFiles`에서 String Table 처리 시도
- 언어 컬럼명이 SystemLanguage enum과 불일치
- id 빈 값 또는 중복
- description이 출력에 포함됨
- pb64 확장자가 `.pb64` (`.asset`이어야 함)
- AssetManager name 캐시 직접 사용

---

## Reference

- NDJSON 저장: `skills/devian/34-ndjson-storage/SKILL.md`
- pb64 저장: `skills/devian/35-pb64-storage/SKILL.md`
- Related: `skills/devian-unity/30-unity-components/12-download-manager/SKILL.md` (다운로드 연동)
- Related: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md` (캐시 금지 규칙)
- Related: `skills/devian/32-json-row-io/SKILL.md` (일반 테이블 포맷)
