# 03-ssot — Unity

Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian-core/03-ssot/SKILL.md

---

## Scope

이 문서는 **Unity UPM 패키지, Packages Sync, Unity 게이트** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../devian-core/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

---

## UPM 전역 설정 (upmConfig) — Hard Rule

**`{buildInputJson}`은 반드시 `upmConfig` 섹션을 포함해야 한다.**

```json
"upmConfig": {
  "sourceDir": "../framework-cs/upm",
  "packageDir": "../framework-cs/apps/UnityExample/Packages"
}
```

| 필드 | 의미 | 필수 |
|------|------|------|
| `sourceDir` | UPM 소스 루트 — 수동 관리 패키지 (upm) | ✅ |
| `packageDir` | Unity Packages 루트 (UnityExample/Packages) | ✅ |

`upmConfig`가 없거나 필드가 누락되면 빌더는 **하드 실패(throw Error)**한다.

---

## UPM Packages Sync 정본 (Hard Rule)

**Packages는 derived output이며 직접 수정 금지.**

| 구분 | 경로 | 역할 |
|------|------|------|
| 정본 (수동) | `framework-cs/upm/{pkg}` | 수동 관리 패키지 원본 |
| 정본 (생성) | `framework-cs/upm/{pkg}` | 빌더가 생성하는 패키지 원본 |
| 복사본 (실행) | `framework-cs/apps/UnityExample/Packages/{pkg}` | Unity 실행용 복사본 |

**Hard DoD: Packages 동기화 불일치 FAIL**

sync 후 아래 조건이면 **즉시 FAIL**:
- `Packages/{pkg}`가 선택된 소스(upm)와 내용이 다름
- 정본 소스에 있는데 Packages에 반영되지 않음
- Packages에서 직접 수정한 코드 발견 (다음 sync에서 덮어써짐)

**필수 검증 대상 패키지:**
- `com.devian.foundation` — Core + Unity 통합 패키지
- `com.devian.samples` — 샘플 패키지

> **WARNING:** `Packages/` 직접 수정은 정책 위반이며, sync 시 손실된다.

---

## UPM 동기화 흐름 (Hard Rule)

빌드 최종 단계에서 다음 동기화가 수행된다:

1. **staging(tempDir)** → **upm**: Domain/Protocol UPM 패키지 생성
2. **upm** → **packageDir**: 최종 동기화

**동기화 규칙:**
- 패키지 단위 clean+copy (packageDir 전체 rm -rf 금지)

**충돌 정책 (HARD RULE):**
- upm에 **동일 `package.json.name`이 있으면 무조건 빌드 FAIL**
- 예외 없음 — 충돌 해결: 패키지 이름 변경 또는 하나 제거

---

## Unity UPM 패키지 구조 (Hard Rule)

**Devian Unity 런타임은 단일 패키지(com.devian.foundation)로 제공한다.**

| 구분 | 경로 | 설명 |
|------|------|------|
| Foundation 패키지 | `framework-cs/upm/com.devian.foundation` | Core + Unity 통합 |

**패키지 내부 폴더 구조 (Hard Rule):**

```
com.devian.foundation/
  Runtime/
    Core/                     # UnityEngine 의존 없는 순수 C# 코드
      Devian.Core.asmdef      # noEngineReferences: true
    Unity/                    # UnityEngine 의존 코드
      Devian.Unity.asmdef
  Editor/
    Devian.Unity.Editor.asmdef
```

**패키지 내부 asmdef (Hard Rule):**

| asmdef | 위치 | namespace | 역할 |
|--------|------|-----------|------|
| `Devian.Core` | `Runtime/Core/` | `Devian` | 순수 C# 런타임 (UnityEngine 의존 없음) |
| `Devian.Unity` | `Runtime/Unity/` | `Devian.Unity` | Unity 어댑터 (UnityEngine 사용) |
| `Devian.Unity.Editor` | `Editor/` | `Devian.Unity` | Unity Editor 전용 |

> **asmdef 분리 정책:**
> - `Devian.Core`는 `noEngineReferences: true`로 UnityEngine 참조를 금지한다.
> - `Devian.Unity`는 `Devian.Core`를 참조한다.

---

## Foundation Package (SSOT)

- 공통 기반 라이브러리는 `com.devian.foundation` UPM 패키지가 SSOT다.
- 이 패키지 안에 `Devian.Core` / `Devian.Unity` asmdef가 존재한다.
- Sound/Voice는 foundation에 포함하지 않고 `com.devian.domain.sound`로 분리 유지한다.

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
- `com.devian.domain.*`
- `com.devian.protocol.*`

---

## Unity C# Compatibility Gate (Hard Rule)

**Unity C# 문법 제한은 [skills/devian-core/04-unity-csharp-compat](../../devian-core/04-unity-csharp-compat/SKILL.md)가 정본이다.**

### DoD (완료 정의) — 하드 게이트

아래 패턴이 적용 범위 경로에서 **1개라도 발견되면 FAIL**:

| 금지 패턴 (정규식) | 탐지 대상 |
|-------------------|-----------|
| `\bclass\s+\w+\s*\(` | class primary constructor |
| `\brecord\b` | record 타입 |
| `\brequired\b` | required 멤버 |
| `^\s*namespace\s+.*;\s*$` | file-scoped namespace |

**검사 대상 경로:**
- `framework-cs/upm/`
- `framework-cs/apps/**/Packages/`
- UPM 패키지 내부의 `Samples~/` 및 템플릿/샘플 코드도 검사 대상에 포함한다.

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

- [Root SSOT](../../devian-core/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Unity Policy](../01-policy/SKILL.md)
- [Unity C# Compat](../../devian-core/04-unity-csharp-compat/SKILL.md)
- [Package Metadata](../04-package-metadata/SKILL.md)
