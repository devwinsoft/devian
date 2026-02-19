# 02-unity-bundles

Status: ACTIVE  
AppliesTo: v10

## Prerequisites

**Unity C# 문법 제한:** 이 문서에서 다루는 모든 UPM 패키지 코드는 `skills/devian/10-module/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 **UnityExample embedded 패키지 묶음(번들)**의 **구성/레이아웃/의존성/asmdef 규약**을 정의한다.

> **주의:** 이 문서는 "패키지"가 아니라 **번들 정책(embedded 패키지 묶음)**을 정의한다.
> 개별 패키지 상세는 각 패키지 스킬(`skills/devian-unity/06-domain-packages/`)을 참조한다.

---

## 목표

- UnityEngine.dll을 외부 .NET 빌드에서 직접 참조하지 않는다.
- UnityExample에 embedded UPM 패키지로 다음을 제공한다:
  - `com.devian.foundation` (Devian 런타임 통합: Core + Unity)
  - `com.devian.ui` (UI 컴포넌트: UIManager, UICanvas, UIFrame, Plugins)
  - `com.devian.domain.common` (Devian.Domain.Common 소스 + Complex PropertyDrawer)
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
| `com.devian.foundation` | Devian 런타임 통합 (Core + Unity) |
| `com.devian.ui` | UI 컴포넌트 (UIManager, UICanvas, UIFrame, Plugins) |
| `com.devian.domain.common` | Devian.Domain.Common 소스 (Complex types + PropertyDrawers) |
| `com.devian.domain.game` | Devian.Domain.Game 소스 (테이블 생성 예제) |
| `com.devian.domain.sound` | Sound/Voice 도메인 |
| `com.devian.samples` | UPM Samples~ 기반 샘플 코드 |

> **패키지 통합 정책 (Hard Rule):**
> - `com.devian.core`, `com.devian.unity`는 더 이상 별도 패키지로 존재하지 않는다.
> - 모든 런타임 기능은 `com.devian.foundation` 단일 패키지에 포함된다.

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## 의존 방향 정책 (핵심)

```
com.devian.foundation (base - Core + Unity 통합)
       ↑
com.devian.ui (UI 컴포넌트 - foundation + domain.common + domain.sound 의존)
       ↑
com.devian.domain.* (module packages - foundation 의존)
```

> **Hard Rule:** `com.devian.foundation` → `com.devian.domain.*` 의존 **금지** (순환 방지)
> **Hard Rule:** `com.devian.foundation` → `com.devian.ui` 의존 **금지** (순환 방지)

---

## asmdef 규약

### Runtime asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | com.devian.foundation/Runtime/Module |
| `Devian.Unity.asmdef` | `Devian.Unity` | `["Devian.Core"]` | com.devian.foundation/Runtime/Unity |
| `Devian.UI.asmdef` | `Devian.UI` | `["Devian.Core", "Devian.Unity", "Devian.Domain.Sound"]` | com.devian.ui/Runtime |
| `Devian.Domain.Common.asmdef` | `Devian.Domain.Common` | `["Devian.Core", "Devian.Unity", "Newtonsoft.Json"]` | com.devian.domain.common |
| `Devian.Domain.Sound.asmdef` | `Devian.Domain.Sound` | `["Devian.Core", "Devian.Unity"]` | com.devian.domain.sound |

### Editor asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Unity.Editor.asmdef` | `Devian.Unity.Editor` | `["Devian.Core", "Devian.Unity"]` | com.devian.foundation/Editor |
| `Devian.UI.Editor.asmdef` | `Devian.UI.Editor` | `["Devian.UI", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.ui/Editor |
| `Devian.Domain.Common.Editor.asmdef` | `Devian.Domain.Common.Editor` | `["Devian.Domain.Common", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.domain.common |
| `Devian.Domain.Sound.Editor.asmdef` | `Devian.Domain.Sound.Editor` | `["Devian.Domain.Sound", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.domain.sound |

---

## dependencies 규약 (package.json)

| 패키지 | dependencies |
|--------|--------------|
| `com.devian.foundation` | `com.unity.addressables` |
| `com.devian.ui` | `com.devian.foundation`, `com.devian.domain.common`, `com.devian.domain.sound` |
| `com.devian.domain.common` | `com.devian.foundation`, `com.unity.nuget.newtonsoft-json` |
| `com.devian.domain.sound` | `com.devian.foundation` |
| `com.devian.domain.game` | `com.devian.foundation`, `com.devian.domain.sound` |
| `com.devian.samples` | (없음) |

---

## SSOT 경로

| 유형 | 경로 |
|------|------|
| 정적 UPM 패키지 (SSOT) | `framework-cs/upm/` |
| 생성 UPM 패키지 | `framework-cs/upm/` |
| UnityExample 최종 패키지 | `framework-cs/apps/UnityExample/Packages/` |

---

## 패키지 동기화 규칙 (Hard Rule)

**UnityExample/Packages는 빌더가 clean+copy로 갱신한다.**

| 정본 | 복사본 | 동작 |
|------|--------|------|
| `upm/{pkg}` | `Packages/{pkg}` | clean + copy |

**수정은 upm/에서만 한다.**

- `Packages/`에서 수정한 코드는 다음 sync에서 덮어써지며, **정책 위반**이다.
- 수동 패키지(`com.devian.foundation`, `com.devian.samples` 등)는 `upm/`에서 수정
- 생성 패키지(`com.devian.domain.common`, `com.devian.protocol.*` 등)는 빌더가 `upm/`에 생성

---

## Reference

- Related: `skills/devian-unity/01-policy/SKILL.md`
- Related: `skills/devian-unity/04-package-metadata/SKILL.md`
- Related: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/06-domain-packages/com.devian.domain.common/SKILL.md`
