# 19-unity-module-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `com.devian.domain.common` UPM 패키지의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian.Domain.Common 소스를 Unity UPM 패키지로 제공한다.
- Common features(Variant, Complex types)를 포함한다.
- Complex PropertyDrawer(Editor)를 포함한다.
- UnityEngine 직접 의존 없이 순수 C# 코드만 포함한다(Editor 코드 제외).

---

## 의존 방향 정책 (핵심)

```
com.devian.core (base - Logger 포함)
       ↑
com.devian.unity (Unity adapters)
       ↑
com.devian.domain.common (이 패키지 - core + unity 의존)
```

> **Hard Rule:** `com.devian.unity` → `com.devian.domain.*` 의존 **금지** (순환 방지)

---

## 패키지 루트 (SSOT)

```
framework-cs/upm/com.devian.domain.common/
```

> **Note**: `framework-cs/apps/UnityExample/Packages/com.devian.domain.common`는 sync 대상(배포/테스트)일 뿐, SSOT가 아니다.

---

## 패키지 레이아웃

```
com.devian.domain.common/
├── package.json
├── Runtime/
│   ├── Devian.Domain.Common.asmdef
│   ├── Generated/
│   │   └── Common.g.cs              (generated)
│   └── Features/
│       ├── Variant.cs
│       └── Complex/
│           ├── CInt.cs
│           ├── CFloat.cs
│           ├── CString.cs
│           └── ComplexUtil.cs
└── Editor/
    ├── Devian.Domain.Common.Editor.asmdef
    ├── Generated/
    │   └── {TableName}_ID.Editor.cs  (keyed table 있을 때 생성)
    └── Complex/
        ├── CIntPropertyDrawer.cs
        ├── CFloatPropertyDrawer.cs
        └── CStringPropertyDrawer.cs
```

> **중요:** Logger는 `com.devian.core`에 위치한다. 이 패키지에는 Logger가 없다.

---

## package.json 정책

| 필드 | 값 |
|------|-----|
| name | `com.devian.domain.common` |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devian Module Common` |
| description | `Devian.Domain.Common runtime for Unity (source)` |
| unity | `2021.3` |
| author.name | `Devian` |
| dependencies | `com.devian.core: 0.1.0`, `com.devian.unity: 0.1.0`, `com.unity.nuget.newtonsoft-json: 3.2.1` |

---

## asmdef 정책

### Runtime asmdef (파일명: `Devian.Domain.Common.asmdef`)

```json
{
  "name": "Devian.Domain.Common",
  "rootNamespace": "Devian.Domain.Common",
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

### Editor asmdef (파일명: `Devian.Domain.Common.Editor.asmdef`)

```json
{
  "name": "Devian.Domain.Common.Editor",
  "rootNamespace": "Devian.Domain.Common.Editor",
  "references": [
    "Devian.Domain.Common",
    "Devian.Unity.Common",
    "Devian.Unity.Common.Editor"
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

> **Note**: 
> - `package.json`의 `dependencies`에는 `com.unity.nuget.newtonsoft-json`을, `asmdef`의 `references`에는 `Newtonsoft.Json`을 지정한다.
> - Editor asmdef는 `Devian.Unity.Common`, `Devian.Unity.Common.Editor`를 참조해야 한다(PropertyDrawer 베이스 클래스 사용).

---

## 포함 파일

### Runtime

| 파일 | 설명 |
|------|------|
| `Runtime/Generated/Common.g.cs` | TableGen으로 생성된 Common 모듈 코드 |
| `Runtime/Features/Variant.cs` | Variant feature 구현 (SSOT: 11-feature-variant) |
| `Runtime/Features/Complex/*.cs` | Complex feature 구현 (SSOT: 13-feature-complex) |

### Editor

| 파일 | 설명 |
|------|------|
| `Editor/Complex/CIntPropertyDrawer.cs` | CInt PropertyDrawer |
| `Editor/Complex/CFloatPropertyDrawer.cs` | CFloat PropertyDrawer |
| `Editor/Complex/CStringPropertyDrawer.cs` | CString PropertyDrawer |
| `Editor/Generated/{TableName}_ID.Editor.cs` | TableID Inspector 바인딩 (keyed table 있을 때 생성) |

> **주의:** Logger는 `com.devian.core/Runtime/Core/Logger.cs`에 위치한다. 이 패키지에 Logger를 포함하지 않는다.

---

## Complex PropertyDrawer 정책

**Complex types(CInt, CFloat, CString)의 PropertyDrawer는 이 패키지의 `Editor/Complex/`에 위치한다.**

- `CIntPropertyDrawer.cs` - CInt 인스펙터 UI
- `CFloatPropertyDrawer.cs` - CFloat 인스펙터 UI  
- `CStringPropertyDrawer.cs` - CString 인스펙터 UI (LocalizationKey 지원)

> **이전 위치와 다름:** 기존에는 `com.devian.unity/Editor/Complex/`에 있었으나, 의존 방향 정책에 따라 이 패키지로 이동됨.

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging에 생성 후 clean+copy로 targetDir에 덮어쓴다.**

- staging에 포함되지 않은 파일은 clean+copy 이후 삭제된다.
- 따라서 Features(Variant/Complex)는 **빌더가 staging에 복사**해야 한다.
- Common 모듈일 때만 `framework-cs/module/Devian.Domain.Common/features/` → staging `Runtime/Features/`로 복사.
- `upmConfig.packageDir`가 UnityExample/Packages를 가리키면 해당 디렉토리는 **generated output**으로 취급된다.

---

## 금지

- **UnityEngine 의존 코드 포함 금지**: 이 패키지의 Runtime 코드는 "공용 코어"로 유지한다.
- **Unity 전용 Sink 포함 금지**: UnityLogSink 등은 `com.devian.unity`에 분리한다.
- Runtime 코드에서 `UnityEngine.*` namespace 직접 참조 금지.
- **Features를 `Common.g.cs`에 생성으로 박아 넣는 방식 금지** (Feature는 수동 소스 유지).
- **clean+copy 정책을 무시하고 targetDir에 수동으로만 파일을 두는 방식 금지** (재빌드 시 삭제됨).
- **Logger 포함 금지**: Logger는 `com.devian.core`에 위치한다.

---

## Editor/Generated 정책

keyed table(primaryKey 있는 테이블)이 있으면 `Editor/Generated/`에 TableID Inspector 바인딩(`{TableName}_ID.Editor.cs`)이 자동 생성된다.

- **베이스 클래스**: `com.devian.unity`이 `EditorID_DrawerBase`, `EditorID_SelectorBase`를 제공
- **Editor asmdef 참조**: `Devian.Unity.Common`, `Devian.Unity.Common.Editor` 필수 (빌더가 자동 패치)

> **공통 도메인 템플릿 규칙**: `skills/devian-upm/20-packages/com.devian.domain.template/SKILL.md` 참조

---

## Reference

- Related: `skills/devian-upm/20-packages/com.devian.domain.template/SKILL.md` (도메인 패키지 공통 규약)
- Related: `skills/devian-upm/02-upm-bundles/SKILL.md`
- Related: `skills/devian-upm/03-package-metadata/SKILL.md`
- Related: `skills/devian-upm/20-packages/com.devian.unity/SKILL.md`
- Related: `skills/devian-common/11-feature-variant/SKILL.md`
- Related: `skills/devian-common/13-feature-complex/SKILL.md`
