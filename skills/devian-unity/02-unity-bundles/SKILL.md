# 02-unity-bundles

Status: ACTIVE  
AppliesTo: v10

## Prerequisites

**Unity C# 문법 제한:** 이 문서에서 다루는 모든 UPM 패키지 코드는 `skills/devian/04-unity-csharp-compat/SKILL.md`를 준수한다 (금지 문법 사용 시 FAIL).

## SSOT

이 문서는 **UnityExample embedded 패키지 묶음(번들)**의 **구성/레이아웃/의존성/asmdef 규약**을 정의한다.

> **주의:** 이 문서는 "패키지"가 아니라 **번들 정책(embedded 패키지 묶음)**을 정의한다.
> 개별 패키지 상세는 각 패키지 스킬(`skills/devian-unity/20-packages/`)을 참조한다.

---

## 목표

- UnityEngine.dll을 외부 .NET 빌드에서 직접 참조하지 않는다.
- UnityExample에 embedded UPM 패키지로 다음을 제공한다:
  - `com.devian.core` (Devian 런타임 통합: Core + Network + Protobuf + Log)
  - `com.devian.domain.common` (Devian.Domain.Common 소스 + Complex PropertyDrawer)
  - `com.devian.unity` (Unity 어댑터: UnityLogSink, AssetManager, TableID Editor, Network 런타임)
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
| `com.devian.core` | Devian 런타임 통합 (Core + Network + Protobuf + Log) |
| `com.devian.domain.common` | Devian.Domain.Common 소스 (Complex types + PropertyDrawers) |
| `com.devian.domain.game` | Devian.Domain.Game 소스 (테이블 생성 예제) |
| `com.devian.unity` | Unity 어댑터 (UnityLogSink, AssetManager, TableID Editor, Network 런타임) |
| `com.devian.samples` | UPM Samples~ 기반 샘플 코드 |

> **패키지 단일화 정책 (Hard Rule):**
> - 이전의 `com.devian.network`, `com.devian.protobuf` 패키지는 삭제되었다.
> - 모든 런타임 기능은 `com.devian.core` 단일 패키지에 포함된다.

## 버전 정책

모든 `com.devian.*` 패키지는 동일한 버전 문자열을 사용한다. (예: `0.1.0`)

---

## 의존 방향 정책 (핵심)

```
com.devian.core (base - 의존 없음)
       ↑
com.devian.unity (Unity adapters - core만 의존)
       ↑
com.devian.domain.* (module packages - core + unity 의존)
```

> **Hard Rule:** `com.devian.unity` → `com.devian.domain.*` 의존 **금지** (순환 방지)

---

## asmdef 규약

### Runtime asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Core.asmdef` | `Devian.Core` | `[]` | com.devian.core |
| `Devian.Unity.asmdef` | `Devian.Unity` | `["Devian.Core"]` | com.devian.unity |
| `Devian.Domain.Common.asmdef` | `Devian.Domain.Common` | `["Devian.Core", "Newtonsoft.Json"]` | com.devian.domain.common |
| `Devian.Domain.Game.asmdef` | `Devian.Domain.Game` | `["Devian.Core", "Devian.Domain.Common"]` | com.devian.domain.game |

### Editor asmdef

| asmdef | name | references | 패키지 |
|--------|------|------------|--------|
| `Devian.Unity.Editor.asmdef` | `Devian.Unity.Editor` | `["Devian.Core", "Devian.Unity"]` | com.devian.unity |
| `Devian.Domain.Common.Editor.asmdef` | `Devian.Domain.Common.Editor` | `["Devian.Domain.Common", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.domain.common |
| `Devian.Domain.Game.Editor.asmdef` | `Devian.Domain.Game.Editor` | `["Devian.Domain.Common", "Devian.Domain.Game", "Devian.Unity", "Devian.Unity.Editor"]` | com.devian.domain.game |

---

## dependencies 규약 (package.json)

| 패키지 | dependencies |
|--------|--------------|
| `com.devian.core` | (없음) |
| `com.devian.unity` | `com.devian.core` |
| `com.devian.domain.common` | `com.devian.core`, `com.devian.unity`, `com.unity.nuget.newtonsoft-json` |
| `com.devian.domain.game` | `com.devian.core`, `com.devian.unity` |
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
- 수동 패키지(`com.devian.core`, `com.devian.unity`, `com.devian.samples` 등)는 `upm/`에서 수정
- 생성 패키지(`com.devian.domain.common`, `com.devian.protocol.*` 등)는 빌더가 `upm/`에 생성

---

## Reference

- Related: `skills/devian-unity/01-unity-policy/SKILL.md`
- Related: `skills/devian-unity/03-package-metadata/SKILL.md`
- Related: `skills/devian-unity/20-packages/com.devian.unity/SKILL.md`
- Related: `skills/devian-unity/20-packages/com.devian.domain.common/SKILL.md`
