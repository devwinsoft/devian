# 21-unity-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Unity 전용 Common 확장 패키지(`com.devian.unity`)의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian Core에 대한 Unity 전용 확장(어댑터)을 제공한다.
- UnityLogSink를 통해 Unity 콘솔에 로그를 출력한다.
- AssetManager를 통해 번들 기반 에셋 로딩 및 Editor 전용 Find 기능을 제공한다.
- TableID Inspector 바인딩의 **베이스 클래스**(EditorID_DrawerBase, EditorID_SelectorBase)를 제공한다.
- **Network 런타임** (NetWsClientBehaviourBase)을 포함한다.
- 이 패키지는 UnityEngine을 의존하므로 Unity 환경에서만 사용 가능하다.

---

## 의존 방향 정책 (핵심)

```
com.devian.core (base)
       ↑
com.devian.unity (이 패키지 - core + addressables 의존)
       ↑
com.devian.domain.* (module packages - unity 의존)
```

### package.json 의존성

```json
{
  "dependencies": {
    "com.devian.core": "0.1.0",
    "com.unity.addressables": "2.7.6"
  }
}
```

> **Hard Rule:** 
> - `com.devian.unity` → `com.devian.domain.*` 의존 **금지** (순환 방지)
> - `com.unity.addressables` 의존은 **DownloadManager** (Addressables Label 기반 다운로드)에 필수

---

## Network 런타임 포함

**NetWsClientBehaviourBase는 `Devian.Unity` 어셈블리에 포함된다.**

- 경로: `Runtime/Network/NetWsClientBehaviourBase.cs`
- 어셈블리: `Devian.Unity` (별도 asmdef 없음)

> **Note:** Network 코드는 별도 어셈블리로 분리하지 않고 `Devian.Unity.asmdef`에 통합되어 있다.

---

## Components

이 패키지에 포함된 컴포넌트의 상세 정책/API는 전용 스킬 문서를 참조한다.

| 컴포넌트 | 설명 | 전용 스킬 |
|----------|------|-----------|
| Singleton | Persistent Singleton (MonoBehaviour 3종 + Pure C# 1종) | `skills/devian-unity/30-unity-components/01-singleton/SKILL.md` |
| PoolManager | AutoSingleton Registry + Type/Name/Active-Inactive 디버깅 하이어라키 + factory.Spawn 확장 | `skills/devian-unity/30-unity-components/02-pool-manager/SKILL.md` |
| AssetManager | AssetBundle 기반 로딩/캐시/언로드 | `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md` |
| NetWsClientBehaviourBase | WebSocket 네트워크 클라이언트 베이스 | `skills/devian-unity/30-unity-components/11-network-client-behaviour/SKILL.md` |
| DownloadManager | Addressables Label 기반 Patch/Download (ResSingleton) | `skills/devian-unity/30-unity-components/12-download-manager/SKILL.md` |

---

## 네임스페이스 정책 (Hard Rule)

**모든 코드는 단일 네임스페이스 `Devian`을 사용한다.**

- Runtime 코드: `namespace Devian`
- Editor 코드: `namespace Devian` (#if UNITY_EDITOR 블록 내부)

> **주의**: `Devian.Unity`, `Devian.Unity.Common` 같은 서브네임스페이스를 사용하지 않는다. 어셈블리명과 네임스페이스는 별개다.

---

## 패키지 루트

```
framework-cs/apps/UnityExample/Packages/com.devian.unity/
```

---

## 패키지 레이아웃

```
com.devian.unity/
├── package.json
├── Runtime/
│   ├── Devian.Unity.asmdef
│   ├── UnityLogSink.cs
│   ├── AssetManager/
│   │   ├── AssetManager.cs
│   │   └── DownloadManager.cs                (ResSingleton - Addressables Patch/Download)
│   ├── Network/
│   │   └── NetWsClientBehaviourBase.cs
│   ├── _Shared/                              (수기 코드 / 생성기 clean+generate 금지)
│   │   ├── UnityMainThread.cs                (공용 내부 헬퍼 - 메인 스레드 감지)
│   │   └── UnityMainThreadDispatcher.cs      (로그 디스패처 - 백그라운드→메인 스레드)
│   ├── Singleton/                            (수기 코드 / 생성기 clean+generate 금지)
│   │   ├── MonoSingleton.cs
│   │   ├── AutoSingleton.cs
│   │   ├── ResSingleton.cs
│   │   └── SimpleSingleton.cs
│   ├── Pool/                                 (수기 코드 / 생성기 clean+generate 금지)
│   │   ├── IPoolable.cs
│   │   ├── IPoolFactory.cs
│   │   ├── PoolOptions.cs
│   │   ├── IPool.cs
│   │   ├── Pool.cs
│   │   ├── PoolManager.cs
│   │   ├── PoolTag.cs
│   │   └── PoolFactoryExtensions.cs
│   └── PoolFactories/                        (수기 코드 / 생성기 clean+generate 금지)
│       ├── InspectorPoolFactory.cs
│       ├── BundlePoolFactory.cs
│       └── BundlePool.cs
└── Editor/
    ├── Devian.Unity.Editor.asmdef
    └── TableId/
        ├── EditorRectUtil.cs
        ├── EditorID_DrawerBase.cs
        └── EditorID_SelectorBase.cs
```

> **중요**: 
> - 이 패키지에는 `Editor/Generated/` 폴더를 생성하지 않는다.
> - **Complex PropertyDrawer(`CInt/CFloat/CString`)는 `com.devian.domain.common/Editor/Complex/`에 위치한다.**
> - **Network 폴더에 별도 asmdef가 없다** - `Devian.Unity.asmdef`에 통합됨.
> - **`_Shared/`, `Singleton/`, `Pool/`, `PoolFactories/`, `AssetManager/` 폴더는 고정 유틸 수기 코드이며 생성기는 절대 clean/generate하지 않는다** (`skills/devian/03-ssot/SKILL.md`의 "Generated Only 정책" 준수).
> - **생성기가 다루는 건 `Runtime/Generated`, `Editor/Generated`뿐**인데, 이 패키지는 `Editor/Generated`를 만들지 않음.
> - **`UnityMainThread`는 `_Shared/`에 1개만 존재** - Singleton/Pool 폴더에 중복 생성 금지
> - **`Runtime/Templates/` 레거시 경로가 존재하면 FAIL**

---

## TableID Editor 바인딩 정책 (Hard Rule)

**이 패키지는 베이스 클래스만 제공하고, 실제 TableID Editor 바인딩은 각 도메인 모듈 패키지에서 생성한다.**

### 베이스 클래스 (이 패키지가 제공)

- `EditorID_DrawerBase<TSelector>` - PropertyDrawer 베이스
- `EditorID_SelectorBase` - ScriptableWizard 베이스
- `EditorRectUtil` - Editor UI 유틸리티

### 생성된 바인딩 (각 도메인 모듈 패키지에 생성)

- **생성 위치**: `com.devian.domain.<domain>/Editor/Generated/{TableName}_ID.Editor.cs`
- **생성 주체**: `build.js` (`generateDomainUpmScaffold`)
- **네임스페이스**: `Devian` 단일 (서브네임스페이스 금지)
- **클래스명 규칙**: `{DomainName}_{TableName}_ID_Selector`, `{DomainName}_{TableName}_ID_Drawer`
- **파일명 규칙**: `{TableName}_ID.Editor.cs`

> **도메인 패키지 생성 규칙 상세**: `skills/devian-unity/20-packages/com.devian.domain.template/SKILL.md` 참조

---

## DoD (완료 정의) — Hard Gate

- [ ] `framework-cs/upm/com.devian.unity/Editor/TableId/` 베이스 파일 존재
- [ ] `framework-cs/upm/com.devian.unity/Editor/Generated/` **존재하지 않음**
- [ ] `framework-cs/upm/com.devian.unity/Editor/Complex/` **존재하지 않음** (module.common으로 이동됨)
- [ ] `framework-cs/upm/com.devian.unity/Runtime/Network/` 에 asmdef 파일 **없음**
- [ ] 각 도메인 모듈 패키지에 TableID Editor 바인딩이 올바르게 생성됨
- [ ] 동기화 후 `UnityExample/Packages/com.devian.unity/Runtime/_Shared/UnityMainThread.cs` 존재 (수기 코드 포함)
- [ ] 동기화 후 `UnityExample/Packages/com.devian.unity/Runtime/_Shared/UnityMainThreadDispatcher.cs` 존재 (수기 코드 포함, 10-unity-main-thread 스킬 참조)
- [ ] UnityLogSink는 Dispatcher를 사용하여 백그라운드 로그를 메인 스레드로 디스패치함
- [ ] 동기화 후 `UnityExample/Packages/com.devian.unity/Runtime/Singleton/*.cs` 4개 파일 존재 (수기 코드 포함)
- [ ] 동기화 후 `UnityExample/Packages/com.devian.unity/Runtime/Pool/*.cs` 8개 파일 존재 (수기 코드 포함)
- [ ] 동기화 후 `UnityExample/Packages/com.devian.unity/Runtime/PoolFactories/*.cs` 3개 파일 존재 (수기 코드 포함)

**수기 코드 정책 (Generated Only 정책 준수):**
- 소스(`framework-cs/upm/com.devian.unity/Runtime/`)에 `_Shared`, `Singleton`, `Pool`, `PoolFactories` 폴더가 **존재해야 함** (수기 코드)
- 생성기는 이 폴더들을 clean/generate하지 않음 - **Generated 폴더만 다룸**
- 동기화 순서: `_Shared` → `Singleton` → `Pool` → `PoolFactories`
- `_Shared` 폴더: `UnityMainThread.cs`, `UnityMainThreadDispatcher.cs` 2개 파일

**공용 헬퍼 (Runtime/_Shared/):**
- `UnityMainThread.cs` - 메인 스레드 검증 (10-unity-main-thread 스킬)
- `UnityMainThreadDispatcher.cs` - 로그 메인 스레드 디스패치 (10-unity-main-thread 스킬)

**Singleton (Runtime/Singleton/):**
- `MonoSingleton.cs`
- `AutoSingleton.cs`
- `ResSingleton.cs`
- `SimpleSingleton.cs`

**Pool (Runtime/Pool/):**
- `IPoolable.cs`
- `IPoolFactory.cs`
- `PoolOptions.cs`
- `IPool.cs`
- `Pool.cs`
- `PoolManager.cs`
- `PoolTag.cs`
- `PoolFactoryExtensions.cs`

**PoolFactories (Runtime/PoolFactories/):**
- `InspectorPoolFactory.cs`
- `BundlePoolFactory.cs`
- `BundlePool.cs`

**FAIL 조건:**
- `com.devian.unity/Editor/Generated/`에 파일이 존재함
- `com.devian.unity/Editor/Complex/`에 파일이 존재함
- `com.devian.unity/Runtime/Network/`에 asmdef 파일이 존재함
- `com.devian.unity/Runtime/Templates/` 레거시 경로가 존재함
- `com.devian.unity/Runtime/Singleton/UnityMainThread.cs`이 존재함 (중복 금지)
- `com.devian.unity/Runtime/Pool/UnityMainThread.cs`이 존재함 (중복 금지)

---

## package.json 정책

| 필드 | 값 |
|------|-----|
| name | `com.devian.unity` |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devian Unity Common` |
| description | `Unity adapter utilities for Devian Core` |
| unity | `2021.3` |
| author.name | `Kim, Hyong Joon` |
| dependencies | `com.devian.core: 0.1.0` |

> **주의:** `com.devian.domain.*` 의존 **금지** (순환 방지)

---

## asmdef 정책

> 아래 JSON 예시에서 `"name"` 및 `"references"` 필드 값은 **어셈블리명**이며, 네임스페이스가 아닙니다.

### Runtime asmdef (파일명: `Devian.Unity.asmdef`)

```json
{
  "name": "Devian.Unity",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Core",
    "Unity.Addressables",
    "Unity.ResourceManager"
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

> **Note:** 
> - Network 폴더의 코드도 이 asmdef에 포함된다 (별도 Network asmdef 없음).
> - `Unity.Addressables`, `Unity.ResourceManager` 참조는 **DownloadManager**에서 Addressables API를 사용하기 위해 필수.

### Editor asmdef (파일명: `Devian.Unity.Editor.asmdef`)

```json
{
  "name": "Devian.Unity.Editor",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Core",
    "Devian.Unity"
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

### UnityLogSink

```csharp
namespace Devian
{
    public class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string tag, string message, Exception? ex = null);
    }
}
```

**멀티스레드 지원 (Main Thread Dispatch):**

UnityLogSink는 멀티스레드 호출을 지원하며, `10-unity-main-thread` 스킬의 Dispatcher를 **의존하여 사용**한다.

- **메인 스레드 호출**: 즉시 `Debug.Log*` 실행 (기존 동작)
- **백그라운드 스레드 호출**: `UnityMainThreadDispatcher` 큐에 적재 → 메인 스레드 `Update()`에서 출력
- Unity API 호출(`Debug.Log` 등)은 **메인 스레드에서만** 수행한다.
- **maxPerFrame 제한**: 프레임당 최대 500개 로그만 처리하여 프레임 드랍 방지

> **Note:** `UnityMainThread`, `UnityMainThreadDispatcher`는 `10-unity-main-thread` 스킬이 소유하며, `_Shared`에 수기 코드로 존재한다. UnityLogSink는 이를 사용만 한다.

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

### AssetManager

> **전용 스킬 문서 참조:** `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md`

### EditorID_DrawerBase / EditorID_SelectorBase

```csharp
namespace Devian
{
    // TableID Inspector 바인딩을 위한 베이스 클래스 (Editor 전용)
    public abstract class EditorID_DrawerBase<TSelector> : PropertyDrawer { ... }
    public abstract class EditorID_SelectorBase : ScriptableWizard { ... }
    public static class EditorRectUtil { ... }
}
```

---

## 사용 예시

```csharp
using Devian;

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
- **서브네임스페이스 사용 금지**: `Devian.Unity`, `Devian.Domain` 등 사용하지 않음. 모든 코드는 `namespace Devian`만 사용.
- **Editor/Generated 생성 금지**: TableID Editor 바인딩은 각 도메인 모듈 패키지에 생성한다.
- **Complex PropertyDrawer 포함 금지**: module.common/Editor/Complex에 위치함.
- **module.* 패키지 의존 금지**: 순환 방지를 위해 core만 의존.
- **Network 폴더에 별도 asmdef 생성 금지**: Devian.Unity에 통합됨.

---

## Reference

- Related: `skills/devian-unity/01-unity-policy/SKILL.md`
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian-unity/03-package-metadata/SKILL.md`
- Related: `skills/devian-unity/10-unity-main-thread/SKILL.md`
- Related: `skills/devian-unity/20-packages/com.devian.domain.common/SKILL.md`
- Related: `skills/devian-unity/30-unity-components/SKILL.md`
- Related: `skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md`
- Related: `skills/devian-unity/30-unity-components/11-network-client-behaviour/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
- Related: `skills/devian-common/13-feature-complex/SKILL.md`
