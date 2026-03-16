# 02-unity-bundles

Status: ACTIVE
AppliesTo: v11

## Prerequisites

**Unity C# 문법 제한:** 이 문서에서 다루는 모든 UPM 패키지 코드는 `skills/devian-unity/05-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 **UnityExample embedded 패키지 묶음(번들)**의 **구성/레이아웃/asmdef 규약**을 정의한다.

> **주의:** 이 문서는 "패키지"가 아니라 **번들 정책(embedded 패키지 묶음)**을 정의한다.
> 도메인 모듈 정책은 `skills/devian/20-domain-common/02-module-policy/SKILL.md`를 참조한다.

---

## 목표

- UnityEngine.dll을 외부 .NET 빌드에서 직접 참조하지 않는다.
- UnityExample에 embedded UPM 패키지로 다음을 제공한다:
  - `com.devian.foundation` (모듈 래핑: Core + 모듈 타입 Editor)
  - `com.devian.samples` (UPM Samples~ 기반 도메인/프로토콜/수동 샘플 코드 — CommonPackage, SoundPackage, GamePackage, MobilePackage, GameProtocol, UIPackage)

## 비목표

- TS 변경 없음.
- UPM 배포(레지스트리/서버)는 다루지 않는다(embedded만).

---

## 패키지 루트 (embedded)

모든 패키지는 아래에 위치한다:

```
framework-cs/apps/UnityExample/Packages/
```

## 구성 패키지 목록

| 패키지 | 역할 |
|--------|------|
| `com.devian.foundation` | 모듈 래핑 (Core + 모듈 타입 Editor) |
| `com.devian.samples` | UPM Samples~ 기반 도메인/프로토콜/수동 샘플 코드 (CommonPackage, SoundPackage, GamePackage, MobilePackage, GameProtocol, UIPackage) |

> **Note:** 모든 도메인 패키지(Common, Sound, Game)는 `com.devian.samples/Samples~/` 하위에 Sample로 제공된다 (독립 `com.devian.domain.*` UPM 패키지 없음).

> 패키지 통합 정책(com.devian.core/unity 금지)은 [03-ssot](../03-ssot/SKILL.md) §Base UPM package를 참조한다.

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## 의존 방향 정책 (핵심)

```
com.devian.foundation (base - Core + 모듈 타입 Editor)
       ↑
com.devian.samples (도메인/프로토콜/수동 샘플 — CommonPackage, SoundPackage, GamePackage, MobilePackage, GameProtocol, UIPackage)
```

> **Hard Rule:** `com.devian.foundation` → `com.devian.samples` 의존 **금지** (순환 방지)

dependencies 상세 테이블은 [04-package-metadata](../04-package-metadata/SKILL.md) §dependencies 정책을 참조한다.

---

## asmdef 규약

### Runtime asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | com.devian.foundation/Runtime/Module |

### Samples~ Runtime asmdef

| asmdef | name | references | 패키지/위치 |
|--------|------|------------|-------------|
| `Devian.Samples.CommonPackage.asmdef` | `Devian.Samples.CommonPackage` | `["Devian.Core", "Unity.Addressables", "Unity.ResourceManager", "Unity.InputSystem", "Newtonsoft.Json"]` | com.devian.samples/Samples~/CommonPackage |
| `Devian.Samples.SoundPackage.asmdef` | `Devian.Samples.SoundPackage` | `["Devian.Core", "Devian.Samples.CommonPackage"]` | com.devian.samples/Samples~/SoundPackage |
| `Devian.Samples.GamePackage.asmdef` | `Devian.Samples.GamePackage` | `["Devian.Core", "Devian.Samples.CommonPackage", "Devian.Samples.SoundPackage"]` | com.devian.samples/Samples~/GamePackage |
| `Devian.Samples.MobilePackage.asmdef` | `Devian.Samples.MobilePackage` | `["Devian.Core", "Devian.Samples.CommonPackage", "Devian.Samples.GamePackage", "Unity.InputSystem", "Devian.Samples.UIPackage", "Unity.Purchasing"]` | com.devian.samples/Samples~/MobilePackage |
| `Devian.Samples.GameProtocol.asmdef` | `Devian.Samples.GameProtocol` | `["Devian.Core", "Devian.Samples.CommonPackage"]` | com.devian.samples/Samples~/GameProtocol |
| `Devian.Samples.UIPackage.asmdef` | `Devian.Samples.UIPackage` | `["Devian.Core", "Devian.Samples.CommonPackage", "Devian.Samples.SoundPackage", "Unity.TextMeshPro"]` | com.devian.samples/Samples~/UIPackage |

### Editor asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Unity.Editor.asmdef` | `Devian.Unity.Editor` | `["Devian.Core", "Devian.Samples.CommonPackage"]` | com.devian.foundation/Editor |

### Samples~ Editor asmdef

| asmdef | name | references | 패키지/위치 |
|--------|------|------------|-------------|
| `Devian.Samples.CommonPackage.Editor.asmdef` | `Devian.Samples.CommonPackage.Editor` | `["Devian.Samples.CommonPackage", "Devian.Unity.Editor", "Devian.Unity", "Unity.InputSystem"]` | com.devian.samples/Samples~/CommonPackage |
| `Devian.Samples.SoundPackage.Editor.asmdef` | `Devian.Samples.SoundPackage.Editor` | `["Devian.Samples.SoundPackage", "Devian.Samples.CommonPackage", "Devian.Samples.CommonPackage.Editor", "Devian.Unity.Editor", "Devian.Unity"]` | com.devian.samples/Samples~/SoundPackage |
| `Devian.Samples.GamePackage.Editor.asmdef` | `Devian.Samples.GamePackage.Editor` | `["Devian.Samples.GamePackage", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.samples/Samples~/GamePackage |
| `Devian.Samples.MobilePackage.Editor.asmdef` | `Devian.Samples.MobilePackage.Editor` | `["Devian.Samples.MobilePackage", "Devian.Core", "Devian.Samples.CommonPackage", "Devian.Samples.GamePackage", "Devian.Unity.Editor"]` | com.devian.samples/Samples~/MobilePackage |
| `Devian.Samples.UIPackage.Editor.asmdef` | `Devian.Samples.UIPackage.Editor` | `["Devian.Samples.UIPackage", "Devian.Samples.CommonPackage", "Devian.Unity.Editor"]` | com.devian.samples/Samples~/UIPackage |

---

## Reference

- [Unity Policy](../01-policy/SKILL.md) — UPM Sync/동기화 규칙
- [Package Metadata](../04-package-metadata/SKILL.md) — package.json 메타데이터/dependencies
- [Root SSOT](../../devian/10-module/03-ssot/SKILL.md) — Foundation Package SSOT
- [Module Policy](../../devian/20-domain-common/02-module-policy/SKILL.md) — 도메인 모듈 정책
