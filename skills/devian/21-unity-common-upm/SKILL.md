# 21-unity-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Unity 전용 Common 확장 패키지(`com.devian.unity.common`)의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian.Module.Common에 대한 Unity 전용 확장(어댑터)을 제공한다.
- UnityLogSink를 통해 Unity 콘솔에 로그를 출력한다.
- AssetManager를 통해 번들 기반 에셋 로딩 및 Editor 전용 Find 기능을 제공한다.
- TableID Inspector 바인딩의 **베이스 클래스**(EditorID_DrawerBase, EditorID_SelectorBase)를 제공한다.
- 이 패키지는 UnityEngine을 의존하므로 Unity 환경에서만 사용 가능하다.

---

## 네임스페이스 정책 (Hard Rule)

**모든 코드는 단일 네임스페이스 `Devian.Unity`를 사용한다.**

- Runtime 코드: `namespace Devian.Unity`
- Editor 코드: `namespace Devian.Unity` (#if UNITY_EDITOR 블록 내부)

> **주의**: `Devian.Unity.Common`, `Devian.Unity.Editor` 같은 서브네임스페이스를 사용하지 않는다.

---

## 패키지 루트

```
framework-cs/apps/UnityExample/Packages/com.devian.unity.common/
```

---

## 패키지 레이아웃

```
com.devian.unity.common/
├── package.json
├── Runtime/
│   ├── Devian.Unity.Common.asmdef
│   ├── UnityLogSink.cs
│   └── AssetManager.cs
└── Editor/
    ├── Devian.Unity.Common.Editor.asmdef
    ├── Complex/
    │   ├── CIntPropertyDrawer.cs
    │   ├── CFloatPropertyDrawer.cs
    │   └── CStringPropertyDrawer.cs
    └── TableId/
        ├── EditorRectUtil.cs
        ├── EditorID_DrawerBase.cs
        └── EditorID_SelectorBase.cs
```

> **중요**: 이 패키지에는 `Editor/Generated/` 폴더를 생성하지 않는다. TableID Editor 바인딩(`*_ID.Editor.cs`)은 각 도메인 모듈 패키지(`com.devian.module.<domain>/Editor/Generated/`)에 생성된다.

---

## TableID Editor 바인딩 정책 (Hard Rule)

**이 패키지는 베이스 클래스만 제공하고, 실제 TableID Editor 바인딩은 각 도메인 모듈 패키지에서 생성한다.**

### 베이스 클래스 (이 패키지가 제공)

- `EditorID_DrawerBase<TSelector>` - PropertyDrawer 베이스
- `EditorID_SelectorBase` - ScriptableWizard 베이스
- `EditorRectUtil` - Editor UI 유틸리티

### 생성된 바인딩 (각 도메인 모듈 패키지에 생성)

- **생성 위치**: `com.devian.module.<domain>/Editor/Generated/{TableName}_ID.Editor.cs`
- **생성 주체**: `build.js` (`generateDomainUpmScaffold`)
- **네임스페이스**: `Devian.Unity` 단일 (서브네임스페이스 금지)
- **클래스명 규칙**: `{DomainName}_{TableName}_ID_Selector`, `{DomainName}_{TableName}_ID_Drawer`
- **파일명 규칙**: `{TableName}_ID.Editor.cs`

### 도메인 모듈 Editor asmdef 참조 요건

각 도메인 모듈 패키지(`com.devian.module.<domain>`)의 Editor asmdef는 다음을 참조해야 한다:

- `Devian.Module.<Domain>` - 해당 도메인 Runtime
- `Devian.Unity.Common` - AssetManager 등
- `Devian.Unity.Common.Editor` - EditorID_* 베이스 클래스

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging → upm → packageDir 파이프라인을 통해 최종 반영된다.**

### 파이프라인 흐름

```
1. 정본 소스(입력):  framework-cs/upm/com.devian.unity.common/**
2. Staging:          {tempDir}/static-com.devian.unity.common/**
3. Materialize:      framework-cs/upm/com.devian.unity.common/** (clean+copy)
4. Final(packageDir): framework-cs/apps/UnityExample/Packages/com.devian.unity.common/** (clean+copy)
```

### 주요 규칙

- **upm → staging**: 빌더가 입력 템플릿을 staging 폴더로 복사
- **staging → upm**: `copyStaticUpmGeneratedContent()`가 materialize
- **upm → packageDir**: `syncUpmToPackageDir()`가 최종 반영 (upm이 정본)
- **Editor/Generated 제거**: staging에서 레거시 Generated 폴더가 있으면 삭제

> **주의**: 이 패키지에는 `Editor/Generated/`를 생성하지 않는다. TableID Editor 바인딩은 각 도메인 모듈 패키지에 생성된다.

---

## DoD (완료 정의) — Hard Gate

- [ ] `framework-cs/upm/com.devian.unity.common/Editor/TableId/` 베이스 파일 존재
- [ ] `framework-cs/upm/com.devian.unity.common/Editor/Generated/` **존재하지 않음**
- [ ] 각 도메인 모듈 패키지에 TableID Editor 바인딩이 올바르게 생성됨

**FAIL 조건:**
- `com.devian.unity.common/Editor/Generated/`에 파일이 존재함

---

## package.json 정책

| 필드 | 값 |
|------|-----|
| name | `com.devian.unity.common` |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devian Unity Common` |
| description | `Unity adapter utilities for Devian.Module.Common` |
| unity | `2021.3` |
| author.name | `Kim, Hyong Joon` |
| dependencies | `com.devian.module.common: 0.1.0` |

---

## asmdef 정책

### Runtime/Devian.Unity.Common.asmdef

```json
{
  "name": "Devian.Unity.Common",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Module.Common"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

### Editor/Devian.Unity.Common.Editor.asmdef

```json
{
  "name": "Devian.Unity.Common.Editor",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Module.Common",
    "Devian.Unity.Common"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

---

## API

### Devian.Unity.UnityLogSink

```csharp
namespace Devian.Unity
{
    public class UnityLogSink : Devian.Module.Common.ILogSink
    {
        public void Write(LogLevel level, string tag, string message, Exception? ex = null);
    }
}
```

**Write 분기 규칙:**

| LogLevel | Unity API |
|----------|-----------|
| Debug | `UnityEngine.Debug.Log(...)` |
| Info | `UnityEngine.Debug.Log(...)` |
| Warn | `UnityEngine.Debug.LogWarning(...)` |
| Error | `UnityEngine.Debug.LogError(...)` |

**출력 포맷:**
- 기본: `[{level}] {tag} - {message}`
- Error + ex: `[{level}] {tag} - {message}\n{ex.ToString()}`

### Devian.Unity.AssetManager

```csharp
namespace Devian.Unity
{
    public static class AssetManager
    {
        // Bundle 로드/언로드
        public static IEnumerator LoadBundle(string key, string bundleFilePath);
        public static void UnloadBundle(string key, bool unloadAllLoadedObjects = false);
        
        // Bundle 에셋 로드 및 캐시
        public static IEnumerator LoadBundleAssets<T>(string key) where T : UnityEngine.Object;
        public static T? GetAsset<T>(string fileName) where T : UnityEngine.Object;
        
        // Editor 전용 (UNITY_EDITOR)
        public static T[] FindAssets<T>(string fileName, params string[] searchDirs) where T : UnityEngine.Object;
        public static GameObject[] FindPrefabs(params string[] searchDirs);
        public static GameObject[] FindPrefabs<T>(params string[] searchDirs) where T : Component;
    }
}
```

### Devian.Unity.EditorID_DrawerBase / EditorID_SelectorBase

```csharp
namespace Devian.Unity
{
    // TableID Inspector 바인딩을 위한 베이스 클래스 (Editor 전용)
    public abstract class EditorID_DrawerBase<TSelector> : PropertyDrawer { ... }
    public abstract class EditorID_SelectorBase : ScriptableWizard { ... }
    public static class EditorRectUtil { ... }
}
```

**Table ID Inspector 로딩 규칙 (Hard Rule):**
- Selector/Drawer 생성물은 TextAsset 로드 시 **`.json` 확장자만 허용**한다.
- DATA 파일은 `ndjson/` 폴더에 `{TableName}.json`(내용은 NDJSON)으로 저장된다.
- Inspector가 `.ndjson` 확장자를 검색/필터링하면 **정책 위반(FAIL)**.

**DoD (Inspector 생성물):**
- `*_ID.Editor.cs` 생성물 내부에 `.EndsWith(".json"` 이 존재해야 **PASS**
- `.EndsWith(".ndjson"` 또는 `".ndjson"` 문자열이 존재하면 **FAIL**

---

## 사용 예시

```csharp
using Devian.Module.Common;
using Devian.Unity;

// Unity 콘솔로 로그 출력 설정
Logger.SetSink(new UnityLogSink());

Logger.Info("Game", "Game started");
Logger.Warn("Auth", "Token expiring soon");
Logger.Error("Net", "Connection failed", exception);
```

---

## 금지

- **core/network/protobuf 코드 포함 금지**: 이 패키지는 확장(어댑터)만 담당한다.
- **자동 설치(런타임 init) 금지**: 정책 미확정이므로 "수동 SetSink"만 제공한다.
- Logger.SetSink()를 자동으로 호출하는 코드 포함 금지.
- **Resources 기반 로딩 금지**: AssetManager는 번들 + Editor Find 전용.
- **서브네임스페이스 사용 금지**: `Devian.Unity.Common`, `Devian.Unity.Editor` 등 사용하지 않음.
- **Editor/Generated 생성 금지**: TableID Editor 바인딩은 각 도메인 모듈 패키지에 생성한다.

---

## Reference

- Related: `skills/devian/15-unity-bundle-upm/SKILL.md`
- Related: `skills/devian/17-upm-package-metadata/SKILL.md`
- Related: `skills/devian/19-unity-module-common-upm/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
- Related: `skills/devian-common/13-feature-complex/SKILL.md`
