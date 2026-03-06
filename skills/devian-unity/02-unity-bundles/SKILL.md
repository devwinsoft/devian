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
  - `com.devian.ui` (UI 컴포넌트: UIManager, UICanvas, UIFrame, Plugins)
  - `com.devian.domain.common` (Unity 런타임 + 도메인 공통)
  - `com.devian.domain.sound` (Sound/Voice 도메인)
  - `com.devian.samples` (UPM Samples~ 기반 샘플 코드)

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
| `com.devian.ui` | UI 컴포넌트 (UIManager, UICanvas, UIFrame, Plugins) |
| `com.devian.domain.common` | Unity 런타임 + 도메인 공통 (Devian.Domain.Common) |
| `com.devian.domain.game` | Devian.Domain.Game 소스 (테이블 생성 예제) |
| `com.devian.domain.sound` | Sound/Voice 도메인 |
| `com.devian.samples` | UPM Samples~ 기반 샘플 코드 |

> 패키지 통합 정책(com.devian.core/unity 금지)은 [03-ssot](../03-ssot/SKILL.md) §Base UPM package를 참조한다.

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## 의존 방향 정책 (핵심)

```
com.devian.foundation (base - Core + 모듈 타입 Editor)
       ↑
com.devian.domain.* (module packages - foundation 의존)
       ↑
com.devian.ui (UI 컴포넌트 - foundation + domain.common + domain.sound 의존)
```

> **Hard Rule:** `com.devian.foundation` → `com.devian.domain.*` 의존 **금지** (순환 방지)
> **Hard Rule:** `com.devian.foundation` → `com.devian.ui` 의존 **금지** (순환 방지)

dependencies 상세 테이블은 [04-package-metadata](../04-package-metadata/SKILL.md) §dependencies 정책을 참조한다.

---

## asmdef 규약

### Runtime asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | com.devian.foundation/Runtime/Module |
| `Devian.UI.asmdef` | `Devian.UI` | `["Devian.Core", "Devian.Domain.Common", "Devian.Domain.Sound"]` | com.devian.ui/Runtime |
| `Devian.Domain.Common.asmdef` | `Devian.Domain.Common` | `["Devian.Core", "Unity.Addressables", "Unity.ResourceManager", "Unity.InputSystem", "Newtonsoft.Json"]` | com.devian.domain.common |
| `Devian.Domain.Sound.asmdef` | `Devian.Domain.Sound` | `["Devian.Core", "Devian.Domain.Common"]` | com.devian.domain.sound |

### Editor asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Unity.Editor.asmdef` | `Devian.Unity.Editor` | `["Devian.Core", "Devian.Domain.Common"]` | com.devian.foundation/Editor |
| `Devian.UI.Editor.asmdef` | `Devian.UI.Editor` | `["Devian.UI", "Devian.Domain.Common", "Devian.Unity.Editor"]` | com.devian.ui/Editor |
| `Devian.Domain.Common.Editor.asmdef` | `Devian.Domain.Common.Editor` | `["Devian.Domain.Common", "Devian.Unity.Editor"]` | com.devian.domain.common |
| `Devian.Domain.Game.Editor.asmdef` | `Devian.Domain.Game.Editor` | `["Devian.Domain.Game", "Devian.Domain.Common", "Devian.Domain.Common.Editor", "Devian.Unity.Editor"]` | com.devian.domain.game |
| `Devian.Domain.Sound.Editor.asmdef` | `Devian.Domain.Sound.Editor` | `["Devian.Domain.Sound", "Devian.Domain.Common", "Devian.Domain.Common.Editor", "Devian.Unity.Editor"]` | com.devian.domain.sound |

---

## Reference

- [Unity Policy](../01-policy/SKILL.md) — UPM Sync/동기화 규칙
- [Package Metadata](../04-package-metadata/SKILL.md) — package.json 메타데이터/dependencies
- [Root SSOT](../../devian/10-module/03-ssot/SKILL.md) — Foundation Package SSOT
- [Module Policy](../../devian/20-domain-common/02-module-policy/SKILL.md) — 도메인 모듈 정책
