# 19-unity-module-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `com.devian.module.common` UPM 패키지의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian.Module.Common 소스를 Unity UPM 패키지로 제공한다.
- Common features(Logger 등)를 포함한다.
- UnityEngine 의존 없이 순수 C# 코드만 포함한다.

---

## 패키지 루트

```
framework-cs/apps/UnityExample/Packages/com.devian.module.common/
```

---

## 패키지 레이아웃

```
com.devian.module.common/
├── package.json
├── Runtime/
│   ├── Devian.Module.Common.asmdef
│   ├── Common.g.cs              (generated)
│   └── Features/
│       ├── Logger.cs
│       ├── Variant.cs
│       └── Complex/
│           ├── CInt.cs
│           ├── CFloat.cs
│           ├── CString.cs
│           └── ComplexUtil.cs
└── Editor/
    └── Devian.Module.Common.Editor.asmdef
```

---

## package.json 정책

| 필드 | 값 |
|------|-----|
| name | `com.devian.module.common` |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devian Module Common` |
| description | `Devian.Module.Common runtime for Unity (source) - Common features` |
| unity | `2021.3` |
| author.name | `Kim, Hyong Joon` |
| dependencies | `com.devian.core: 0.1.0`, `com.unity.nuget.newtonsoft-json: 3.2.1` |

---

## asmdef 정책

### Runtime/Devian.Module.Common.asmdef

```json
{
  "name": "Devian.Module.Common",
  "rootNamespace": "Devian.Module.Common",
  "references": [
    "Devian.Core",
    "Newtonsoft.Json"
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

> **Note**: `package.json`의 `dependencies`에는 `com.unity.nuget.newtonsoft-json`을, `asmdef`의 `references`에는 `Newtonsoft.Json`을 지정한다. 이 둘은 성격이 다르며 둘 다 필요하다.

---

## 포함 파일

| 파일 | 설명 |
|------|------|
| `Runtime/Common.g.cs` | TableGen으로 생성된 Common 모듈 코드 |
| `Runtime/Features/Logger.cs` | Logger feature 구현 (SSOT: 12-feature-logger) |
| `Runtime/Features/Variant.cs` | Variant feature 구현 (SSOT: 11-feature-variant) |
| `Runtime/Features/Complex/*.cs` | Complex feature 구현 (SSOT: 13-feature-complex) |

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging에 생성 후 clean+copy로 targetDir에 덮어쓴다.**

- staging에 포함되지 않은 파일은 clean+copy 이후 삭제된다.
- 따라서 Features(Logger/Variant/Complex)는 **빌더가 staging에 복사**해야 한다.
- Common 모듈일 때만 `framework-cs/modules/Devian.Module.Common/features/` → staging `Runtime/Features/`로 복사.
- `upmTargetDir`가 UnityExample/Packages를 가리키면 해당 디렉토리는 **generated output**으로 취급된다.

---

## 금지

- **UnityEngine 의존 코드 포함 금지**: 이 패키지는 "공용 코어"로 유지한다.
- **Unity 전용 Sink 포함 금지**: UnityLogSink 등은 `com.devian.unity.common`에 분리한다.
- 코드에서 `UnityEngine.*` namespace 직접 참조 금지.
- **Features를 `Common.g.cs`에 생성으로 박아 넣는 방식 금지** (Feature는 수동 소스 유지).
- **clean+copy 정책을 무시하고 targetDir에 수동으로만 파일을 두는 방식 금지** (재빌드 시 삭제됨).
- **Editor/Generated 폴더 생성 금지**: TableID Inspector 바인딩(`*_ID.Editor.cs`)은 이 패키지가 아닌 `com.devian.unity.common/Editor/Generated/`에서 생성한다.
- **UnityEditor 의존 파일 포함 금지**: Editor 폴더에는 asmdef만 존재해야 하며, UnityEditor 의존 코드는 `com.devian.unity.common`이 담당한다.

---

## Reference

- Related: `skills/devian/15-unity-bundle-upm/SKILL.md`
- Related: `skills/devian/17-upm-package-metadata/SKILL.md`
- Related: `skills/devian/21-unity-common-upm/SKILL.md`
- Related: `skills/devian-common/11-feature-variant/SKILL.md`
- Related: `skills/devian-common/12-feature-logger/SKILL.md`
- Related: `skills/devian-common/13-feature-complex/SKILL.md`
