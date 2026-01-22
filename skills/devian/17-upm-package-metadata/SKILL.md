# 17-upm-package-metadata

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
- 예: `com.devian.core`, `com.devian.network`, `com.devian.protobuf`, `com.devian.module.common`, `com.devian.unity.network`, `com.devian.unity.common`

### version

- 번들 구성 패키지들은 동일 버전 문자열을 사용한다.
- 예: `0.1.0`

### unity

- 최소 Unity 버전은 동일하게 고정한다.
- 예: `2021.3`

### displayName

- 사람이 읽기 쉬운 이름으로 고정한다.
- 패턴 예:
  - `Devian Core`
  - `Devian Network`
  - `Devian Protobuf`
  - `Devian Module Common`
  - `Devian Unity Network`
  - `Devian Unity Common`

### description

- 1줄로 역할을 명확히 적는다.
- 예:
  - `"Devian.Core runtime for Unity (source)"`
  - `"Devian.Network runtime for Unity (source)"`
  - `"Devian.Protobuf runtime for Unity (source) + Google.Protobuf dll"`
  - `"Devian.Module.Common runtime for Unity (source) - Common features"`
  - `"Unity adapter for Devian.Network (MonoBehaviours)"`
  - `"Unity adapter utilities for Devian.Module.Common"`

### author

- `author.name`은 다음 값으로 고정한다:
  - `Kim, Hyong Joon`
- `author.url` / `email`은 현재 정책에서 사용하지 않는다(비워둔다).

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
| `com.devian.unity.network` | `com.devian.network`, `com.devian.core`, `com.devian.protobuf`, `com.devian.module.common` |
| `com.devian.unity.common` | `com.devian.module.common` |
| `com.devian.module.common` | `com.devian.core`, `com.unity.nuget.newtonsoft-json` |
| `com.devian.network` | `com.devian.core` |
| `com.devian.protobuf` | `com.devian.core` |
| `com.devian.core` | (없음) |

---

## samples 정책 (Samples~ 제공 시)

> ⚠️ **DEPRECATED**: UPM Samples~ 방식은 더 이상 권장하지 않습니다.
>
> **새로운 정책:** `skills/devian-templates/01-templates-policy/SKILL.md`
>
> 샘플/예제 코드는 `framework-cs/upm-src/com.devian.templates/Samples~/`에서 Templates로 관리합니다.

### (참고용) 기존 Samples~ 규칙

`Samples~` 폴더를 제공하는 패키지는 **반드시** `package.json`에 `samples` 배열을 선언해야 한다.
이 필드가 없으면 Unity Package Manager에서 샘플 설치 UI가 표시되지 않는다.

### Builder samples metadata sync 요구사항 (참고용)

**참고:** 대부분의 패키지에서 Samples~는 제거되었습니다.

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
- `com.devian.unity` 메타 패키지 생성/유지 금지 (deprecated)

---

## Reference

- Related: `skills/devian/15-unity-bundle-upm/SKILL.md`
- Related: `skills/devian/19-unity-module-common-upm/SKILL.md`
- Related: `skills/devian/21-unity-common-upm/SKILL.md`
