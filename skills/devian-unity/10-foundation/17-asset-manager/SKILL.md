# 17-asset-manager

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `AssetManager` 컴포넌트의 **API, 정책, 검증 규칙**을 정의한다.

---

## 목적/범위

**Addressables 기반 로딩/캐시 + Resources 로딩(옵션) + Editor Find를 제공하는 Unity 전용 컴포넌트.**

- **런타임(필수)**: Addressables로 라벨/키 기반 에셋 로드 → 캐시에 등록 → 이름으로 조회
- **런타임(옵션)**: Resources 로드/언로드 (캐시 포함)
- **에디터**: `UNITY_EDITOR` 전용 Find 메서드 (AssetDatabase 기반)

> **Note:** 이 문서에서 "Bundle"은 **파일 기반 AssetBundle이 아니라 Addressables의 label/key 묶음**을 의미한다.

---

## 소스 경로

| 위치 | 경로 |
|------|------|
| UPM 소스 | `framework-cs/upm/com.devian.foundation/Runtime/Unity/AssetManager/AssetManager.cs` |
| UnityExample | `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Runtime/Unity/AssetManager/AssetManager.cs` |

---

## API

```csharp
namespace Devian
{
    public static class AssetManager
    {
        // ====================================================================
        // Addressables (Bundle 의미 = Label/Key)
        // ====================================================================
        
        /// <summary>
        /// Addressables에서 단일 에셋을 로드하고 캐시한다.
        /// </summary>
        public static IEnumerator LoadBundleAsset<T>(string key) where T : UnityEngine.Object;
        
        /// <summary>
        /// Addressables에서 label/key에 해당하는 모든 에셋을 로드하고 캐시한다.
        /// </summary>
        public static IEnumerator LoadBundleAssets<T>(string key) where T : UnityEngine.Object;
        
        /// <summary>
        /// Addressables에서 key AND lang (intersection)에 해당하는 에셋을 로드하고 캐시한다.
        /// </summary>
        public static IEnumerator LoadBundleAssets<T>(string key, SystemLanguage lang) where T : UnityEngine.Object;
        
        /// <summary>
        /// 지정된 key로 로드된 에셋들을 캐시에서 제거하고 Addressables handle을 해제한다.
        /// </summary>
        public static IEnumerable<string> UnloadBundleAssets(string key);
        
        // ====================================================================
        // Cache Access (즉시 반환)
        // ====================================================================
        
        /// <summary>
        /// 파일명으로 캐시된 에셋을 가져온다 (확장자 제거, 대소문자 무시).
        /// 탐색 순서: Bundle 캐시 → Resource 캐시
        /// </summary>
        public static T? GetAsset<T>(string fileName) where T : UnityEngine.Object;
        
        /// <summary>
        /// GetAsset의 별칭 (기존 코드 호환용).
        /// </summary>
        public static T? LoadAsset<T>(string fileName) where T : UnityEngine.Object;
        
        // ====================================================================
        // Resources (옵션)
        // ====================================================================
        
        /// <summary>
        /// Resources 폴더에서 에셋을 로드하고 캐시한다.
        /// </summary>
        public static T? LoadResourceAsset<T>(string filePath, SystemLanguage lang = SystemLanguage.Unknown)
            where T : UnityEngine.Object;
        
        /// <summary>
        /// Resources 폴더 하위 디렉토리에서 모든 에셋을 로드하고 캐시한다.
        /// </summary>
        public static T[] LoadResourceAssets<T>(string searchDir, SystemLanguage lang = SystemLanguage.Unknown)
            where T : UnityEngine.Object;
        
        /// <summary>
        /// 캐시된 Resource 에셋을 언로드한다.
        /// </summary>
        public static void UnloadResourceAsset<T>(string fileName) where T : UnityEngine.Object;
        
        /// <summary>
        /// 디렉토리에 해당하는 캐시된 Resource 에셋들을 언로드한다.
        /// </summary>
        public static IEnumerable<string> UnloadResourceAssets<T>(string searchDir) where T : UnityEngine.Object;
        
        // ====================================================================
        // Cleanup
        // ====================================================================
        
        /// <summary>
        /// 모든 캐시된 번들과 리소스를 정리한다.
        /// </summary>
        public static void ClearAll();
        
        // ====================================================================
        // Editor-Only Find Methods (UNITY_EDITOR)
        // ====================================================================
        
#if UNITY_EDITOR
        /// <summary>
        /// 프로젝트에서 파일명으로 에셋을 검색한다 (에디터 전용).
        /// </summary>
        public static T[] FindAssets<T>(string fileName, params string[] searchDirs) 
            where T : UnityEngine.Object;
        
        /// <summary>
        /// 프로젝트에서 모든 프리팹을 검색한다 (에디터 전용).
        /// </summary>
        public static GameObject[] FindPrefabs(params string[] searchDirs);
        
        /// <summary>
        /// 특정 컴포넌트를 가진 프리팹을 검색한다 (에디터 전용).
        /// </summary>
        public static GameObject[] FindPrefabs<T>(params string[] searchDirs) 
            where T : Component;
#endif
    }
}
```

---

## Hard Rules (정책)

### 1. Addressables 기반 (필수)

**`LoadBundleAsset(s)`는 Addressables API를 사용한다.**

- `LoadBundleAssets<T>(key)`: `Addressables.LoadAssetsAsync<T>(key, onEachLoaded)` 사용
- `LoadBundleAssets<T>(key, lang)`: `Addressables.MergeMode.Intersection`으로 key + lang 교집합 로드
- `UnloadBundleAssets(key)`: 해당 key의 handle을 `Addressables.Release`로 해제

### 2. 캐시 키 규칙 (NormalizeAssetName)

**에셋 키는 `NormalizeAssetName(name)` = `Path.GetFileNameWithoutExtension(name).ToLowerInvariant()`로 정규화한다.**

```csharp
private static string NormalizeAssetName(string name)
{
    return Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
}

// 예시
"TestSheet.json" → "testsheet"
"Cube.prefab" → "cube"
```

- 대소문자 무시 (case-insensitive)
- 확장자 제거
- 중복 이름(type별)은 오류 로그 후 등록 거부 (기존 에셋 우선, handle leak 방지)

### 3. 네이밍 예외 (@로 시작하는 에셋)

**에셋의 원본 이름(`asset.name`)이 `@`로 시작하면 캐시에 등록하지 않고 handle을 즉시 해제한다.**

```csharp
// 정규화 전 원본 asset.name 기준으로 체크
if (asset.name.StartsWith("@"))
{
    Addressables.Release(handle);
    yield break; // skip registration
}
```

> **Note:** `assetKey`(정규화 후)가 아니라 `asset.name`(원본) 기준이다. 정규화는 캐시 등록 직전에만 수행된다.

### 4. Resources 허용 범위

**Resources는 명시 API(`LoadResourceAsset(s)` / `UnloadResourceAsset(s)`)로만 로드/정리한다.**

- `GetAsset<T>` 탐색 순서: **Bundle(Addressables) 캐시 → Resource 캐시**
- 무분별한 `Resources.Load` 직접 호출은 권장하지 않음

### 5. 파일 AssetBundle 로딩 제거

**파일 경로 기반 AssetBundle 로딩(`AssetBundle.LoadFromFileAsync`)은 지원하지 않는다.**

- `LoadBundle(key, bundleFilePath)` 형태의 API 없음
- Addressables가 카탈로그/그룹을 통해 번들을 관리함

### 6. 동일 key 재로드 정책 (Handle Leak 방지)

**이미 로드된 key로 `LoadBundleAsset` 또는 `LoadBundleAssets`를 다시 호출하면 경고만 출력하고 무시한다.**

```csharp
// 이미 로드된 key인 경우
if (mBundles.ContainsKey(key))
{
    Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
    yield break;  // 중복 로드 방지 → handle leak 방지
}
```

**중복 asset name(type별) 발견 시:**

```csharp
if (typeDict.ContainsKey(assetKey))
{
    Debug.LogError($"[AssetManager] Duplicate asset name '{assetKey}' for type {type.Name}. Ignoring.");
    Addressables.Release(handle);  // 기존 에셋 우선, 새 handle 해제
    yield break;
}
```

> **Note:** `LoadBundleAssets(key, lang)` 오버로드도 동일하게 `key`로 중복 체크 및 저장한다. 언로드는 항상 `UnloadBundleAssets(key)`로 수행.

---

## 사용 예시

### DownloadManager + AssetManager 연동

```csharp
using Devian;
using UnityEngine;

public class BootSequence : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1. DownloadManager로 다운로드 정책 수행
        DownloadManager.Load("Devian/DownloadManager");
        var dm = DownloadManager.Instance;
        
        yield return dm.PatchProc(
            info => Debug.Log($"Total: {info.TotalSize} bytes"),
            err => Debug.LogError(err)
        );
        
        yield return dm.DownloadProc(
            progress => Debug.Log($"Progress: {progress * 100:F1}%"),
            () => Debug.Log("Download complete"),
            err => Debug.LogError(err)
        );
        
        // 2. AssetManager로 Addressables 에셋 로드
        yield return AssetManager.LoadBundleAssets<GameObject>("prefabs");
        yield return AssetManager.LoadBundleAssets<TextAsset>("table-ndjson");
        
        // 3. 캐시에서 즉시 조회
        var cube = AssetManager.LoadAsset<GameObject>("Cube");
        var testSheet = AssetManager.GetAsset<TextAsset>("TestSheet");
        
        if (cube != null)
        {
            Instantiate(cube);
        }
        
        if (testSheet != null)
        {
            Debug.Log(testSheet.text);
        }
    }
    
    void OnDestroy()
    {
        // 정리
        AssetManager.ClearAll();
    }
}
```

### Resources 로딩 (옵션)

```csharp
// Resources 폴더에서 로드 (Addressables가 아닌 경우)
var config = AssetManager.LoadResourceAsset<TextAsset>("Config/settings");

// 캐시에서 조회 (Bundle + Resources 모두 탐색)
var asset = AssetManager.GetAsset<TextAsset>("settings");

// 정리
AssetManager.UnloadResourceAsset<TextAsset>("settings");
```

---

## String Table Caveat (Hard Rule)

**AssetManager는 에셋을 "이름(fileName)" 기준으로 캐시한다.**

String Table은 언어별로 같은 TableName 파일이 존재하므로, AssetManager 캐시를 직접 재사용하면 **언어 충돌**이 발생한다.

### 금지 패턴

```csharp
// FAIL: AssetManager로 String Table 로드
yield return AssetManager.LoadBundleAsset<TextAsset>("string/ndjson/Korean/UIText");
var text = AssetManager.GetAsset<TextAsset>("UIText"); // 언어 충돌!
```

### 올바른 패턴

**String Table은 `TableManager` + `ST_{TableName}`을 사용하고, `(format, language, tableName)` 키로 별도 캐시한다.**

```csharp
// CORRECT: TableManager / ST_ 사용
yield return ST_UIText.PreloadAsync("ndjson", SystemLanguage.Korean);
var text = ST_UIText.Get("ndjson", SystemLanguage.Korean, "greeting");
```

> **Reference**: `skills/devian-common/14-string-table/SKILL.md`, `skills/devian-unity/10-foundation/10-table-manager/SKILL.md`

---

## DoD (검증 가능)

### PASS 조건

- [ ] `AssetManager/AssetManager.cs` (UPM + UnityExample) 최상단 SSOT가 이 문서를 가리킴
- [ ] `AssetManager/AssetManager.cs`가 `Addressables.LoadAssetsAsync`를 사용
- [ ] `AssetManager/AssetManager.cs`가 `Addressables.Release(handle)`로 언로드
- [ ] `LoadAsset<T>`가 `GetAsset<T>`의 alias로 존재
- [ ] 파일 AssetBundle 로딩 코드(`AssetBundle.LoadFromFileAsync`) 없음

### FAIL 조건

- `AssetBundle.LoadFromFileAsync` 코드가 존재
- `LoadBundle(key, bundleFilePath)` 형태의 API가 존재
- SSOT 주석이 다른 문서를 가리킴

---

## Reference

- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/10-foundation/18-download-manager/SKILL.md` (다운로드 정책)
- Related: `skills/devian-common/14-string-table/SKILL.md` (String Table 규약)
- Related: `skills/devian-unity/01-policy/SKILL.md` (component policy)
