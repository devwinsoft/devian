# 10-asset-manager

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `AssetManager` 컴포넌트의 **API, 정책, 검증 규칙**을 정의한다.

---

## 목적/범위

**AssetBundle 기반 런타임 로딩/캐시/언로드를 제공하는 Unity 전용 컴포넌트.**

- **런타임**: AssetBundle 로드, 에셋 캐시, 언로드 기능
- **에디터**: `UNITY_EDITOR` 전용 Find 메서드 (AssetDatabase 기반)
- **금지**: Resources 기반 로딩 없음

---

## 소스 경로

| 위치 | 경로 |
|------|------|
| UPM 소스 | `framework-cs/upm/com.devian.unity/Runtime/AssetManager.cs` |
| UnityExample | `framework-cs/apps/UnityExample/Packages/com.devian.unity/Runtime/AssetManager.cs` |

---

## API

```csharp
namespace Devian
{
    public static class AssetManager
    {
        // ====================================================================
        // Bundle Load/Unload
        // ====================================================================
        
        /// <summary>
        /// AssetBundle을 파일 경로에서 로드하고 지정된 key로 등록한다.
        /// </summary>
        /// <param name="key">번들을 식별하는 고유 키</param>
        /// <param name="bundleFilePath">번들 파일의 전체 경로</param>
        public static IEnumerator LoadBundle(string key, string bundleFilePath);
        
        /// <summary>
        /// 번들을 언로드하고 캐시된 에셋을 제거한다.
        /// </summary>
        /// <param name="key">번들 키</param>
        /// <param name="unloadAllLoadedObjects">true면 로드된 모든 오브젝트도 파괴</param>
        public static void UnloadBundle(string key, bool unloadAllLoadedObjects = false);
        
        // ====================================================================
        // Bundle Asset Loading
        // ====================================================================
        
        /// <summary>
        /// 로드된 번들에서 타입 T의 모든 에셋을 로드하고 캐시한다.
        /// </summary>
        /// <typeparam name="T">에셋 타입</typeparam>
        /// <param name="key">번들 키 (LoadBundle로 먼저 로드해야 함)</param>
        public static IEnumerator LoadBundleAssets<T>(string key) where T : UnityEngine.Object;
        
        /// <summary>
        /// 파일명으로 캐시된 에셋을 가져온다 (확장자 제거, 대소문자 무시).
        /// </summary>
        /// <typeparam name="T">에셋 타입</typeparam>
        /// <param name="fileName">에셋 파일명 (확장자 포함/미포함)</param>
        /// <returns>에셋 또는 null</returns>
        public static T? GetAsset<T>(string fileName) where T : UnityEngine.Object;
        
        /// <summary>
        /// 모든 캐시된 번들과 에셋을 정리한다.
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

### 1. Resources 기반 로딩 금지

**AssetManager는 AssetBundle + Editor Find 전용이다.**

- `Resources.Load`, `Resources.LoadAsync` 등 사용 금지
- 코드 내 `Resources.` 문자열이 존재하면 **FAIL**

### 2. 동일 key 재로드 정책

**이미 로드된 key로 `LoadBundle`을 다시 호출하면 경고만 출력하고 무시한다.**

```csharp
if (mBundles.ContainsKey(key))
{
    Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
    yield break;
}
```

- Overwrite 아님, 단순 무시 + 경고
- 기존 번들 교체가 필요하면 `UnloadBundle` 후 다시 로드

### 3. Unload 정책

**`UnloadBundle(key, unloadAllLoadedObjects)`**

- `unloadAllLoadedObjects = false` (기본): 번들만 해제, 로드된 오브젝트는 유지
- `unloadAllLoadedObjects = true`: 번들 해제 + 로드된 모든 오브젝트 파괴
- 캐시 딕셔너리에서 해당 번들의 에셋 키도 제거

### 4. 에셋 키 규칙

- 에셋 키는 **파일명(확장자 제거, 소문자)**로 관리
- `GetAsset<T>("TestSheet.json")` → 키: `"testsheet"`
- 대소문자 구분 없음 (case-insensitive)

---

## 사용 예시

```csharp
using Devian;
using UnityEngine;

public class BundleLoader : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1. 번들 로드
        yield return AssetManager.LoadBundle("tables", "/path/to/tables.bundle");
        
        // 2. 에셋 로드 및 캐시
        yield return AssetManager.LoadBundleAssets<TextAsset>("tables");
        
        // 3. 캐시에서 에셋 가져오기
        var asset = AssetManager.GetAsset<TextAsset>("TestSheet.json");
        if (asset != null)
        {
            Debug.Log(asset.text);
        }
        
        // 4. 정리
        AssetManager.UnloadBundle("tables", unloadAllLoadedObjects: true);
    }
}
```

---

## DoD (검증 가능)

### PASS 조건

- [ ] `AssetManager.cs` (UPM + UnityExample) 최상단 SSOT가 이 문서를 가리킴
- [ ] `AssetManager.cs` 내 `Resources.` 문자열 0건

### FAIL 조건

- `AssetManager.cs` 내 `Resources.Load`, `Resources.LoadAsync` 등 존재
- SSOT 주석이 다른 문서를 가리킴

---

## Reference

- Related: `skills/devian-common-upm/20-packages/com.devian.unity/SKILL.md` (패키지 컨텍스트)
- Related: `skills/devian-common-upm/01-upm-policy/SKILL.md` (component policy)
