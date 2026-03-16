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

- `com.devian.foundation` — Core + 모듈 타입 Editor 패키지
- `com.devian.samples` — 도메인/프로토콜/수동 샘플 패키지 (CommonPackage, SoundPackage, GamePackage, MobilePackage, GameProtocol, UIPackage)

> Sync 규칙 자체는 [Unity Policy](../01-policy/SKILL.md) §SSOT 원칙이 정본이다.

---

## UPM 동기화 충돌 정책 (Hard Rule)

upm에 **동일 `package.json.name`이 있으면 무조건 빌드 FAIL**.

- 예외 없음 — 충돌 해결: 패키지 이름 변경 또는 하나 제거

---

## Foundation 패키지 구조 (Hard Rule)

**`com.devian.foundation`은 모듈 래핑 + 모듈 타입 Editor만 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| Foundation 패키지 | `framework-cs/upm/com.devian.foundation` | Core(모듈) + 모듈 타입 Editor |

**패키지 내부 폴더 구조:**

```
com.devian.foundation/
  Runtime/
    Module/                     # devian/10-module 래핑 (순수 C#)
      Devian.Core.asmdef        # noEngineReferences: true
  Editor/
    Devian.Unity.Editor.asmdef  # Complex/VersionNumber Drawer
```

**패키지 내부 asmdef:**

| asmdef | 위치 | namespace | 역할 |
|--------|------|-----------|------|
| `Devian.Core` | `Runtime/Module/` | `Devian` | 순수 C# 런타임 (UnityEngine 의존 없음) |
| `Devian.Unity.Editor` | `Editor/` | `Devian.Unity` | 모듈 타입 Drawer (Complex/VersionNumber) |

> **asmdef 분리 정책:**
> - `Devian.Core`는 `noEngineReferences: true`로 UnityEngine 참조를 금지한다.
> - Unity 런타임 컴포넌트는 `com.devian.samples/Samples~/CommonPackage/`(`Devian.Samples.CommonPackage` asmdef)에 위치한다.

---

## Hard Rule: Base UPM package is com.devian.foundation only

- `com.devian.core`, `com.devian.unity` UPM 패키지는 존재하지 않는다.
- 모든 `com.devian.*` 패키지의 dependencies에서 `com.devian.core`, `com.devian.unity` 사용은 금지이며, 반드시 `com.devian.foundation`을 사용한다.
- 위반 시 빌드는 즉시 FAIL이다.

---

## samplePackages (Hard Rule)

- `samplePackages`는 샘플 패키지 목록이다.
- `samplePackages`에는 `com.devian.samples`만 허용한다.
- 라이브러리, 도메인, 프로토콜은 절대 포함하지 않는다.
- 위반 시 빌드는 즉시 FAIL이어야 한다.

**금지 패키지 목록 (samplePackages에 넣으면 Hard FAIL):**
- `com.devian.foundation`
- `com.devian.domain.*` (레거시, 삭제 완료)
- `com.devian.protocol.*` (레거시, 삭제 완료)
- `com.devian.ui` (레거시, UIPackage 샘플로 이관 완료)

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
- [Package Metadata](../04-package-metadata/SKILL.md) — package.json 메타데이터/dependencies
