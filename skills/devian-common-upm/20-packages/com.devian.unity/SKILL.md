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
com.devian.unity (이 패키지 - core만 의존)
       ↑
com.devian.module.* (module packages - unity 의존)
```

> **Hard Rule:** `com.devian.unity` → `com.devian.module.*` 의존 **금지** (순환 방지)

---

## Network 런타임 포함

**NetWsClientBehaviourBase는 `Devian.Unity.Common` 어셈블리에 포함된다.**

- 경로: `Runtime/Network/NetWsClientBehaviourBase.cs`
- 어셈블리: `Devian.Unity.Common` (별도 asmdef 없음)

> **Note:** Network 코드는 별도 어셈블리로 분리하지 않고 `Devian.Unity.Common.asmdef`에 통합되어 있다.

---

## Components

이 패키지에 포함된 컴포넌트의 상세 정책/API는 전용 스킬 문서를 참조한다.

| 컴포넌트 | 설명 | 전용 스킬 |
|----------|------|-----------|
| AssetManager | AssetBundle 기반 로딩/캐시/언로드 | `skills/devian-common-upm/30-unity-components/10-asset-manager/SKILL.md` |
| NetWsClientBehaviourBase | WebSocket 네트워크 클라이언트 베이스 | `skills/devian-common-upm/30-unity-components/11-network-client-behaviour/SKILL.md` |

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
│   ├── Devian.Unity.Common.asmdef
│   ├── UnityLogSink.cs
│   ├── AssetManager.cs
│   └── Network/
│       └── NetWsClientBehaviourBase.cs
└── Editor/
    ├── Devian.Unity.Common.Editor.asmdef
    └── TableId/
        ├── EditorRectUtil.cs
        ├── EditorID_DrawerBase.cs
        └── EditorID_SelectorBase.cs
```

> **중요**: 
> - 이 패키지에는 `Editor/Generated/` 폴더를 생성하지 않는다.
> - **Complex PropertyDrawer(`CInt/CFloat/CString`)는 `com.devian.module.common/Editor/Complex/`에 위치한다.**
> - **Network 폴더에 별도 asmdef가 없다** - `Devian.Unity.Common.asmdef`에 통합됨.

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
- **네임스페이스**: `Devian` 단일 (서브네임스페이스 금지)
- **클래스명 규칙**: `{DomainName}_{TableName}_ID_Selector`, `{DomainName}_{TableName}_ID_Drawer`
- **파일명 규칙**: `{TableName}_ID.Editor.cs`

---

## DoD (완료 정의) — Hard Gate

- [ ] `framework-cs/upm/com.devian.unity/Editor/TableId/` 베이스 파일 존재
- [ ] `framework-cs/upm/com.devian.unity/Editor/Generated/` **존재하지 않음**
- [ ] `framework-cs/upm/com.devian.unity/Editor/Complex/` **존재하지 않음** (module.common으로 이동됨)
- [ ] `framework-cs/upm/com.devian.unity/Runtime/Network/` 에 asmdef 파일 **없음**
- [ ] 각 도메인 모듈 패키지에 TableID Editor 바인딩이 올바르게 생성됨

**FAIL 조건:**
- `com.devian.unity/Editor/Generated/`에 파일이 존재함
- `com.devian.unity/Editor/Complex/`에 파일이 존재함
- `com.devian.unity/Runtime/Network/`에 asmdef 파일이 존재함

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

> **주의:** `com.devian.module.*` 의존 **금지** (순환 방지)

---

## asmdef 정책

> 아래 JSON 예시에서 `"name"` 및 `"references"` 필드 값은 **어셈블리명**이며, 네임스페이스가 아닙니다.

### Runtime asmdef (파일명: `Devian.Unity.Common.asmdef`)

```json
{
  "name": "Devian.Unity.Common",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Core"
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

> **Note:** Network 폴더의 코드도 이 asmdef에 포함된다 (별도 Network asmdef 없음).

### Editor asmdef (파일명: `Devian.Unity.Common.Editor.asmdef`)

```json
{
  "name": "Devian.Unity.Common.Editor",
  "rootNamespace": "Devian.Unity",
  "references": [
    "Devian.Core",
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

> **전용 스킬 문서 참조:** `skills/devian-common-upm/30-unity-components/10-asset-manager/SKILL.md`

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
- **서브네임스페이스 사용 금지**: `Devian.Unity`, `Devian.Module` 등 사용하지 않음. 모든 코드는 `namespace Devian`만 사용.
- **Editor/Generated 생성 금지**: TableID Editor 바인딩은 각 도메인 모듈 패키지에 생성한다.
- **Complex PropertyDrawer 포함 금지**: module.common/Editor/Complex에 위치함.
- **module.* 패키지 의존 금지**: 순환 방지를 위해 core만 의존.
- **Network 폴더에 별도 asmdef 생성 금지**: Devian.Unity.Common에 통합됨.

---

## Reference

- Related: `skills/devian-common-upm/01-upm-policy/SKILL.md`
- Related: `skills/devian-common-upm/02-upm-bundles/SKILL.md`
- Related: `skills/devian-common-upm/03-package-metadata/SKILL.md`
- Related: `skills/devian-common-upm/20-packages/com.devian.module.common/SKILL.md`
- Related: `skills/devian-common-upm/30-unity-components/SKILL.md`
- Related: `skills/devian-common-upm/30-unity-components/10-asset-manager/SKILL.md`
- Related: `skills/devian-common-upm/30-unity-components/11-network-client-behaviour/SKILL.md`
- Related: `skills/devian-common-feature/12-feature-logger/SKILL.md`
- Related: `skills/devian-common-feature/13-feature-complex/SKILL.md`
