# 04-package-metadata

Status: ACTIVE  
AppliesTo: v10

## SSOT

이 문서는 Devian UPM 패키지(`com.devian.*`)의 package.json 메타데이터 정책에 대한 단일 정본(SSOT)이다.

---

## 적용 범위

- UnityExample embedded packages:
  - `framework-cs/apps/UnityExample/Packages/com.devian.*/package.json`

---

## 필수 메타데이터 정책 (강제)

### name

- 접두어: `com.devian.`
- 예: `com.devian.foundation`, `com.devian.domain.common`, `com.devian.samples`

> **패키지 통합 정책 (Hard Rule):**
> - `com.devian.core`, `com.devian.unity`는 더 이상 별도 패키지로 존재하지 않는다.
> - 모든 런타임 기능은 `com.devian.foundation` 단일 패키지에 포함된다.

### version

- 번들 구성 패키지들은 동일 버전 문자열을 사용한다.
- 예: `0.1.0`

### unity

- 최소 Unity 버전은 동일하게 고정한다.
- 예: `2021.3`

### displayName

- 사람이 읽기 쉬운 이름으로 고정한다.
- 패턴 예:
  - `Devian Foundation` (통합 런타임)
  - `Devian Domain Common`
  - `Devian Samples`

### description

- 1줄로 역할을 명확히 적는다.
- 예:
  - `"Devian Foundation - Core + Unity unified package"`
  - `"Devian.Domain.Common runtime for Unity"`
  - `"Templates for Devian framework"`

### author

- `author.name`은 `Devian`으로 통일한다.
- `author.url` / `email`은 현재 정책에서 사용하지 않는다(비워둔다).

```json
"author": {
  "name": "Devian"
}
```

---

## 권장 메타데이터 정책 (선택)

필요 시 아래 항목을 추가할 수 있다(없어도 PASS):

- `keywords`: `["Devian", "Network", "Protobuf", "Unity"]`
- `documentationUrl`
- `changelogUrl`
- `licensesUrl`

---

## dependencies 정책

runtime 패키지는 필요한 최소 의존만 선언한다:

| 패키지 | dependencies |
|--------|--------------|
| `com.devian.foundation` | `com.unity.addressables` |
| `com.devian.domain.common` | `com.devian.foundation`, `com.unity.nuget.newtonsoft-json` |
| `com.devian.domain.game` | `com.devian.foundation`, `com.devian.domain.sound` |
| `com.devian.domain.sound` | `com.devian.foundation` |
| `com.devian.samples` | (없음) |

> **의존 방향 정책:**
> - `com.devian.domain.*` → `com.devian.foundation` 의존 (domain이 foundation을 의존)
> - `com.devian.foundation` → `com.devian.domain.*` 의존 **금지** (순환 방지)
> - 모든 `com.devian.domain.*` 패키지는 `com.devian.foundation`을 의존한다.
> - Newtonsoft.Json이 필요한 패키지만 `com.unity.nuget.newtonsoft-json`을 추가한다.

---

## Samples~ 정책 (Samples~ 제공 시)

정책: `skills/devian-unity-samples/01-policy/SKILL.md`

`Samples~` 폴더를 제공하는 패키지는 `package.json`에 `samples` 배열을 선언해야 한다.
(Unity Package Manager UI 노출을 위해 필수)

### Builder samples metadata sync 요구사항

Samples~가 존재하는 패키지에 한해 적용:

- `Samples~` 폴더가 존재하고 샘플 하위 폴더가 있으면, Builder는 `package.json`의 `samples` 필드를 자동으로 동기화해야 한다.
- `syncSamplesMetadata()` 호출은 **반드시** `Samples~` 폴더가 target 디렉토리에 복사된 **이후**에 실행되어야 한다.
- 이 순서가 지켜지지 않으면 metadata sync가 빈 폴더를 읽어 `samples` 필드가 누락될 수 있다.

### samples 항목 구조

| 필드 | 필수 | 설명 |
|------|------|------|
| `displayName` | **필수** | Package Manager UI에 표시될 샘플 이름 |
| `description` | 권장 | 샘플 설명 (UI에 표시) |
| `path` | **필수** | `Samples~/...` 상대경로 (폴더명과 대소문자까지 정확히 일치해야 함) |

### 예시

```json
"samples": [
  {
    "displayName": "Basic Ws Client",
    "description": "Minimal WebSocket usage sample.",
    "path": "Samples~/BasicWsClient"
  }
]
```

---

## JSON 포맷 규칙

- 2-space indent
- trailing comma 없음
- key 순서 권장:
  1. `name`
  2. `version`
  3. `displayName`
  4. `description`
  5. `unity`
  6. `author`
  7. `dependencies` (있을 때만)
  8. `samples` (Samples~ 제공 시)

---

## 금지

- `author.name`을 임의로 변경 금지
- 패키지마다 `unity` 최소버전이 달라지게 만들지 말 것
- `com.devian.*` 외 패키지의 package.json은 수정 금지
- `com.devian.foundation` → `com.devian.domain.*` 의존 금지 (순환 방지)

---

## Reference

- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian-unity/06-domain-packages/com.devian.domain.common/SKILL.md`
- Related: `skills/devian-core/03-ssot/SKILL.md` (Foundation Package SSOT)
