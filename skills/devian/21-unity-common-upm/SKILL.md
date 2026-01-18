# 21-unity-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Unity 전용 Common 확장 패키지(`com.devian.unity.common`)의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian.Module.Common에 대한 Unity 전용 확장(어댑터)을 제공한다.
- UnityLogSink를 통해 Unity 콘솔에 로그를 출력한다.
- 이 패키지는 UnityEngine을 의존하므로 Unity 환경에서만 사용 가능하다.

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
│   └── UnityLogSink.cs
└── Editor/
    ├── Devian.Unity.Common.Editor.asmdef
    └── Complex/
        ├── CIntPropertyDrawer.cs
        ├── CFloatPropertyDrawer.cs
        └── CStringPropertyDrawer.cs
```

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging에 생성 후 clean+copy로 targetDir에 덮어쓴다.**

- staging에 포함되지 않은 파일은 clean+copy 이후 삭제된다.
- Editor 폴더(PropertyDrawer 등)는 **빌더가 staging에 복사**해야 한다.
- 정본 소스: `framework-cs/upm-src/com.devian.unity.common/Editor/`

> **주의**: `com.devian.module.common`에는 Editor 코드를 두지 않는다 (서버 빌드/UnityEditor 의존 분리).

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
  "rootNamespace": "Devian.Unity.Common",
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
  "rootNamespace": "Devian.Unity.Common.Editor",
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

### UnityLogSink

```csharp
namespace Devian.Unity.Common
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

---

## 사용 예시

```csharp
using Devian.Module.Common;
using Devian.Unity.Common;

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

---

## Reference

- Related: `skills/devian/15-unity-bundle-upm/SKILL.md`
- Related: `skills/devian/17-upm-package-metadata/SKILL.md`
- Related: `skills/devian/19-unity-module-common-upm/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
- Related: `skills/devian-common/13-feature-complex/SKILL.md`
