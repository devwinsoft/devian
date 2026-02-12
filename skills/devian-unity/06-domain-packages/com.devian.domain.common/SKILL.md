# 19-unity-module-common-upm

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 `com.devian.domain.common` UPM 패키지의 **레이아웃/asmdef/메타데이터/의존성** 규약을 정의한다.

---

## 목표

- Devian.Domain.Common 소스를 Unity UPM 패키지로 제공한다.
- Common features(Variant)를 포함한다.
- UnityEngine 직접 의존 없이 순수 C# 코드만 포함한다(Editor 코드 제외).

---

## 의존 방향 정책 (핵심)

```
com.devian.foundation (base - Core + Unity 통합)
       ↑
com.devian.domain.common (이 패키지 - foundation 의존)
```

> **Hard Rule:** `com.devian.foundation` → `com.devian.domain.*` 의존 **금지** (순환 방지)

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
│   ├── Module/
│   │   └── Core/
│   │       ├── CoreError.cs          (에러 정보 컨테이너)
│   │       └── CoreResult.cs         (성공/실패 결과 타입)
│   └── Features/
│       └── Variant.cs
└── Editor/
    ├── Devian.Domain.Common.Editor.asmdef
    └── Generated/
        └── {TableName}_ID.Editor.cs  (keyed table 있을 때 생성)
```

> **중요:** Log는 `com.devian.foundation`에 위치한다. 이 패키지에는 Log가 없다.

---

## package.json 정책

| 필드 | 값 |
|------|-----|
| name | `com.devian.domain.common` |
| version | `0.1.0` (다른 com.devian.* 패키지와 동일) |
| displayName | `Devian Domain Common` |
| description | `Devian.Domain.Common runtime for Unity (source)` |
| unity | `2021.3` |
| author.name | `Devian` |
| dependencies | `com.devian.foundation: 0.1.0`, `com.unity.nuget.newtonsoft-json: 3.2.1` |

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
    "Devian.Unity",
    "Devian.Unity.Editor"
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
> - Editor asmdef는 `Devian.Unity`, `Devian.Unity.Editor`를 참조해야 한다(PropertyDrawer 베이스 클래스 사용).

---

## 포함 파일

### Runtime

| 파일 | 설명 |
|------|------|
| `Runtime/Generated/Common.g.cs` | TableGen으로 생성된 Common 모듈 코드 (ErrorClientType enum 포함) |
| `Runtime/Module/Core/CoreError.cs` | 에러 정보 컨테이너. 공식 네임스페이스: `Devian.Domain.Common` |
| `Runtime/Module/Core/CoreResult.cs` | 성공/실패 결과 타입. 공식 네임스페이스: `Devian.Domain.Common` |
| `Runtime/Features/Variant.cs` | Variant feature 구현 (SSOT: 32-variable-variant) |

> **CoreError/CoreResult는 Domain.Common의 기본 타입이며, 공식 네임스페이스는 `Devian.Domain.Common`이다.**
> 소스 SSOT: `framework-cs/module/Devian.Domain.Common/src/Core/`
> 빌더 sync: `syncDomainCommonCoreToUpmDomainCommon()`가 module → UPM 동기화를 수행한다.

### Editor

| 파일 | 설명 |
|------|------|
| `Editor/Generated/{TableName}_ID.Editor.cs` | TableID Inspector 바인딩 (keyed table 있을 때 생성) |

> **주의:** Log는 `com.devian.foundation/Runtime/Module/Core/Logger.cs`에 위치한다. 이 패키지에 Log를 포함하지 않는다.

---

## 빌더 생성 정책 (Hard Rule)

**이 UPM 패키지는 빌더(`build.js`)가 staging에 생성 후 clean+copy로 targetDir에 덮어쓴다.**

- staging에 포함되지 않은 파일은 clean+copy 이후 삭제된다.
- 따라서 Features(Variant)는 **빌더가 staging에 복사**해야 한다.
- Common 모듈일 때만 `framework-cs/module/Devian.Domain.Common/features/` → staging `Runtime/Features/`로 복사.
- Complex는 `com.devian.foundation`으로 이동됨 (skills/devian-core/31-variable-complex/SKILL.md 참조).
- `upmConfig.packageDir`가 UnityExample/Packages를 가리키면 해당 디렉토리는 **generated output**으로 취급된다.

---

## CoreResult / CoreError API

CoreError/CoreResult는 Domain.Common의 기본 타입이며, 공식 네임스페이스는 `Devian.Domain.Common`이다.

### CoreResult 주 사용 시그니처

```csharp
// 주 사용 — 프로덕션 코드에서는 이 시그니처만 사용한다.
CoreResult<T>.Failure(ErrorClientType errorType, string message)

// Deprecated (Obsolete) — 내부/호환용. 사용 금지.
[Obsolete] CoreResult<T>.Failure(string code, string message)
```

> **프로덕션 코드에서는 `Failure(string, string)`을 사용하지 않는다(사용처 0개 유지).**
> 에러 식별자는 ERROR_CLIENT 테이블에서 생성되는 `ErrorClientType`를 사용한다.

### CoreError 생성자

```csharp
// 주 사용
new CoreError(ErrorClientType errorType, string message, string? details = null)

// Deprecated (Obsolete) — 내부/호환용. 사용 금지.
[Obsolete] new CoreError(string code, string message, string? details = null)
```

---

## ErrorClientType SSOT

- `CommonTable.xlsx`의 `ERROR_CLIENT` 시트는 `ErrorClientType` enum의 SSOT이다.
- 빌더가 ERROR_CLIENT의 `id` 컬럼으로 `ErrorClientType` enum을 생성한다 (`Common.g.cs`).
- LoginManager / CloudSaveManager / LocalSaveManager / FirebaseManager / PurchaseManager의 에러 코드는 `ErrorClientType` 항목으로 관리한다.
- 새 에러 코드가 필요하면 ERROR_CLIENT 테이블에 행을 추가하고 빌더를 실행한다.

---

## 금지

- **UnityEngine 의존 코드 포함 금지**: 이 패키지의 Runtime 코드는 "공용 코어"로 유지한다.
- **Unity 전용 Sink 포함 금지**: UnityLogSink 등은 `com.devian.foundation`에 분리한다.
- Runtime 코드에서 `UnityEngine.*` namespace 직접 참조 금지.
- **Features를 `Common.g.cs`에 생성으로 박아 넣는 방식 금지** (Feature는 수동 소스 유지).
- **clean+copy 정책을 무시하고 targetDir에 수동으로만 파일을 두는 방식 금지** (재빌드 시 삭제됨).
- **Log 포함 금지**: Log는 `com.devian.foundation`에 위치한다.

---

## Editor/Generated 정책

keyed table(primaryKey 있는 테이블)이 있으면 `Editor/Generated/`에 TableID Inspector 바인딩(`{TableName}_ID.Editor.cs`)이 자동 생성된다.

- **베이스 클래스**: `com.devian.foundation`이 `BaseEditorID_Drawer`, `BaseEditorID_Selector`를 제공
- **Editor asmdef 참조**: `Devian.Unity`, `Devian.Unity.Editor` 필수 (빌더가 자동 패치)

> **공통 도메인 템플릿 규칙**: `skills/devian-unity/06-domain-packages/com.devian.domain.template/SKILL.md` 참조

---

## Reference

- Related: `skills/devian-unity/06-domain-packages/com.devian.domain.template/SKILL.md` (도메인 패키지 공통 규약)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian-unity/04-package-metadata/SKILL.md`
- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-core/32-variable-variant/SKILL.md`
- Related: `skills/devian-core/31-variable-complex/SKILL.md`
