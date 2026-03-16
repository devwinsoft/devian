# 03-ssot — Unity

Status: ACTIVE
AppliesTo: v11
ParentSSOT: skills/devian/10-module/03-ssot/SKILL.md

---

## Scope

이 문서는 **Unity UPM 패키지 구조, 빌드 동기화, 게이트** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../devian/10-module/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

**Sync 정책:** UPM 경로/동기화/미러 규칙은 [Unity Policy](../01-policy/SKILL.md)가 정본이다.

---

## UPM 전역 설정 (upmConfig) — Hard Rule

**`upmConfig`는 `{projectConfigJson}` (예: `input/build_config.json`)에 존재해야 한다.**

`{buildInputJson}` (예: `build_input.json`)에 `upmConfig`가 존재하면 **FAIL**.

```json
// {projectConfigJson} 예시 구조
{
  "configVersion": 1,
  "upmConfig": {
    "sourceDir": "../framework-cs/upm",
    "packageDir": "../framework-cs/apps/UnityExample/Packages"
  }
}
```

| 필드 | 의미 | 필수 |
|------|------|------|
| `sourceDir` | UPM 소스 루트 — 수동 관리 패키지 (upm) | ✅ |
| `packageDir` | Unity Packages 루트 (UnityExample/Packages) | ✅ |

`upmConfig`가 없거나 필드가 누락되면 빌더는 **하드 실패(throw Error)**한다.

---

## 필수 검증 대상 패키지 (Hard Rule)

Sync 후 아래 패키지는 반드시 upm ↔ Packages 일치를 검증한다:

- `com.devian.foundation` — Core + 모듈 타입 Editor + 도메인/프로토콜/수동 샘플 (Samples~: CommonPackage, SoundPackage, GamePackage, MobilePackage, GameProtocol, UIPackage)

> Sync 규칙 자체는 [Unity Policy](../01-policy/SKILL.md) §SSOT 원칙이 정본이다.

---

## UPM 동기화 충돌 정책 (Hard Rule)

upm에 **동일 `package.json.name`이 있으면 무조건 빌드 FAIL**.

- 예외 없음 — 충돌 해결: 패키지 이름 변경 또는 하나 제거

---

## Foundation 패키지 구조 (Hard Rule)

**`com.devian.foundation`은 모듈 래핑 + 모듈 타입 Editor + Samples~(도메인/프로토콜/수동 샘플)를 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| Foundation 패키지 | `framework-cs/upm/com.devian.foundation` | Core(모듈) + 모듈 타입 Editor + Samples~ |

**패키지 내부 폴더 구조:**

```
com.devian.foundation/
  Runtime/
    Module/                     # devian/10-module 래핑 (순수 C#)
      Devian.Core.asmdef        # noEngineReferences: true
  Editor/
    Devian.Unity.Editor.asmdef  # Complex/VersionNumber Drawer
  Samples~/                     # 도메인/프로토콜/수동 샘플
    CommonPackage/
    SoundPackage/
    GamePackage/
    MobilePackage/
    GameProtocol/
    UIPackage/
```

**패키지 내부 asmdef:**

| asmdef | 위치 | namespace | 역할 |
|--------|------|-----------|------|
| `Devian.Core` | `Runtime/Module/` | `Devian` | 순수 C# 런타임 (UnityEngine 의존 없음) |
| `Devian.Unity.Editor` | `Editor/` | `Devian.Unity` | 모듈 타입 Drawer (Complex/VersionNumber) |

> **asmdef 분리 정책:**
> - `Devian.Core`는 `noEngineReferences: true`로 UnityEngine 참조를 금지한다.
> - Unity 런타임 컴포넌트는 `com.devian.foundation/Samples~/CommonPackage/`(`Devian.Samples.CommonPackage` asmdef)에 위치한다.

---

## Hard Rule: Base UPM package is com.devian.foundation only

- `com.devian.core`, `com.devian.unity` UPM 패키지는 존재하지 않는다.
- 모든 `com.devian.*` 패키지의 dependencies에서 `com.devian.core`, `com.devian.unity` 사용은 금지이며, 반드시 `com.devian.foundation`을 사용한다.
- 위반 시 빌드는 즉시 FAIL이다.

---

## Sample Package 상수 (Hard Rule)

- 샘플 패키지 대상은 `com.devian.foundation` 하나뿐이며, 빌더 내부 상수(`UPM_FOUNDATION`)로 고정한다.
- 설정 파일(`{projectConfigJson}`)에 `samplePackages` 키는 **불필요하며 존재하지 않는다.**
- `staticUpmPackages` 키도 금지이며 존재 시 빌드 FAIL.

---

## foundationVersion — Foundation 패키지 버전 (Hard Rule)

### 목적

`com.devian.foundation`의 버전을 `{projectConfigJson}`에서 단일 관리한다.
빌더가 이 값으로 `package.json`의 `version` 필드를 덮어쓰고, SampleSync 경로 해석에도 사용한다.

### 설정

```json
"foundationVersion": "0.1.0"
```

- `{projectConfigJson}`에 **필수**로 지정한다.
- 생략 시 빌드 **FAIL**.

### 빌더 동작

1. **package.json 동기화**: `com.devian.foundation/package.json`의 `version` 필드를 `foundationVersion` 값으로 덮어쓴다.
2. **DEFAULT_VERSIONS 대체**: 빌더 내부 `DEFAULT_VERSIONS` 상수를 이 값으로 대체한다.
3. **sampleFolder 경로**: `{sampleFolder}/{displayName}/{foundationVersion}/{sampleName}/`으로 해석한다.

### SSOT 원칙

- `foundationVersion`이 버전의 **단일 정본**이다.
- `package.json`의 `version` 필드는 빌더가 매 빌드 시 동기화하는 **파생 값**이다.
- 빌더 코드에 버전을 하드코딩하지 않는다.

---

## sampleFolder — Assets/Samples Generated 동기화 (Hard Rule)

### 목적

빌드 시 테이블/프로토콜/contract(JSON)에서 생성되는 코드(`Generated/` 폴더)를 `Assets/Samples` 경로에도 동기화한다.
수동 코드(non-generated)의 전체 패치는 이 동기화의 범위가 아니다.

### 설정

```json
"sampleFolder": "../framework-cs/apps/UnityExample/Assets/Samples"
```

- `{projectConfigJson}`에 선택적으로 지정한다.
- 생략 시 이 동기화 단계를 건너뛴다 (기존 호환성 유지).

### 경로 해석

```
{sampleFolder}/{displayName}/{version}/{sampleName}/
```

| 플레이스홀더 | 출처 | 예시 |
|-------------|------|------|
| `{sampleFolder}` | `{projectConfigJson}` 설정 값 | `../framework-cs/apps/UnityExample/Assets/Samples` |
| `{displayName}` | `com.devian.foundation/package.json` → `displayName` | `Devian Foundation` |
| `{version}` | `{projectConfigJson}` → `foundationVersion` | `0.1.0` |
| `{sampleName}` | `Samples~/` 하위 폴더명 (= `package.json` samples[].path 기준) | `CommonPackage`, `GameProtocol` 등 |

### 동기화 범위 (Generated Only)

**빌드가 생성하는 `Generated/` 폴더만 동기화한다.** 수동 코드는 건드리지 않는다.

복사 대상:
- `Runtime/Generated/**` — `.cs` 파일만 복사 (`.meta` 보존)
- `Editor/Generated/**` — `.cs` 파일만 복사 (`.meta` 보존)

### 동기화 조건

- **대상 Sample 폴더가 존재할 때만** 동기화한다.
- `{sampleFolder}/{displayName}/{version}/{sampleName}/`이 존재하지 않으면 skip (아직 Unity에서 Import하지 않은 상태).
- Generated 폴더 자체가 없는 Sample (예: `MobilePackage`, `UIPackage` — 수동 코드만 있음)은 자연히 skip.

### .meta 보존 정책

- `.cs` 파일만 덮어쓴다. 기존 `.meta` 파일은 건드리지 않는다.
- 새 `.cs` 파일이 추가되어 `.meta`가 없으면 Unity가 다음 실행 시 자동 생성한다.
- 빌더가 `.meta`를 생성/삭제/수정하지 않는다.

### 실행 시점

Phase 4 (Sync: upm → packageDir) 이후에 실행한다.

### 전체 패치 (수동 코드 포함)

수동 코드를 포함한 전체 패치는 다음 두 경우에만 수행한다:
- Unity Package Manager에서 수동 Import
- 사용자가 3-path-mirror 전체 복사를 명시적으로 요청

---

## Unity C# Compatibility Gate (Hard Rule)

**Unity C# 문법 제한은 [05-unity-csharp-compat](../05-unity-csharp-compat/SKILL.md)가 정본이다.**

이 문서에서는 금지 패턴/검사 경로를 재정의하지 않는다.

---

## Table ID Inspector 생성물 Gate (Hard Rule)

**Table ID Inspector 생성물은 `.json` 확장자 필터를 사용해야 한다.**

검사 대상 경로:
- `framework-cs/upm/**/Editor/Generated/*.cs`
- `framework-cs/apps/**/Packages/**/Editor/Generated/*.cs`

**Hard FAIL:**
- 위 대상에서 문자열 `".ndjson"` 발견 시 **FAIL**
- 정본: `.EndsWith(".json"` 형태여야 함

---

## See Also

- [Root SSOT](../../devian/10-module/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Unity Policy](../01-policy/SKILL.md) — UPM Sync/미러/경로 규칙
- [Unity Bundles](../02-unity-bundles/SKILL.md) — 패키지 묶음/asmdef/의존 방향
- [Unity C# Compat](../05-unity-csharp-compat/SKILL.md) — C# 문법 제한
- [Package Policy](../04-package-policy/SKILL.md) — package.json 메타데이터/Samples~ 정책
