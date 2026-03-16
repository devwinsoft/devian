# 04-package-policy

Status: ACTIVE
AppliesTo: v11
Type: Policy

## Scope

Devian UPM 패키지(`com.devian.*`)의 **package.json 메타데이터 정책**과 **Samples~ 구조/생성/빌드 통합 정책**을 통합 정의한다.

> **Samples~의 역할:**
> - Samples~는 UPM 표준 방식으로 templates를 배포하는 메커니즘
> - Templates는 사용자(개발자)가 **Import 후 수정**해서 사용하는 것이 목적 ("editable source distribution")
> - Import된 샘플은 프로젝트 Assets 폴더로 복사됨 (원본은 Packages 내 유지)
> - sync 동작 시 Packages 내 원본만 갱신됨, Assets로 복사된 사용자 수정본은 보존

**적용 대상:**
- `framework-cs/apps/UnityExample/Packages/com.devian.*/package.json`
- `framework-cs/upm/com.devian.samples/Samples~/`

**Prerequisites:**
- Unity C# 문법 제한: `skills/devian-unity/05-unity-csharp-compat/SKILL.md`를 준수한다 (위반 시 FAIL).

---

## package.json 메타데이터 정책 (강제)

### 필수 필드

| 필드 | 정책 |
|------|------|
| `name` | 접두어 `com.devian.` 필수 |
| `version` | 모든 `com.devian.*` 패키지 동일 버전 |
| `unity` | 최소 Unity 버전 동일 고정 |
| `displayName` | 사람이 읽기 쉬운 이름 |
| `description` | 1줄 역할 설명 |
| `author` | `{ "name": "Devian" }` 고정 |

> 패키지 통합 정책(com.devian.core/unity 금지)은 [03-ssot](../03-ssot/SKILL.md) §Base UPM package를 참조한다.

### 권장 필드 (선택)

- `keywords`, `documentationUrl`, `changelogUrl`, `licensesUrl`

### JSON 포맷 규칙

- 2-space indent, trailing comma 없음
- key 순서: `name` → `version` → `displayName` → `description` → `unity` → `author` → `dependencies` → `samples`

---

## dependencies 정책

runtime 패키지는 필요한 최소 의존만 선언한다:

| 패키지 | dependencies |
|--------|--------------|
| `com.devian.foundation` | `com.unity.addressables`, `com.unity.nuget.newtonsoft-json` |
| `com.devian.samples` | `com.devian.foundation`, `com.unity.inputsystem` |

> 의존 방향 정책은 [02-unity-bundles](../02-unity-bundles/SKILL.md) §의존 방향 정책을 참조한다.
> Newtonsoft.Json이 필요한 패키지만 `com.unity.nuget.newtonsoft-json`을 추가한다.

---

## Samples~ 구조 정책 (Hard Rules)

### 샘플 소스 위치 (Hard Rule)

샘플 코드는 **반드시** `framework-cs/upm/<packageName>/Samples~/...`에서만 작성한다.

**금지:**
- `framework-cs/apps/UnityExample/Assets/**` 아래에 샘플 스크립트 생성/수정 금지
- `framework-cs/apps/UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물이므로 덮어씌워짐)

### Runtime/Editor 분리 (Hard Rule)

모든 샘플은 **반드시** `Runtime/`과 `Editor/` 폴더로 분리해야 한다.

**필수 구조:**
```
Samples~/<SampleName>/
├── README.md
├── Runtime/
│   └── Devian.Samples.<SampleName>.asmdef
└── Editor/
    └── Devian.Samples.<SampleName>.Editor.asmdef  ← includePlatforms: ["Editor"]
```

**금지:**
- Runtime 코드에 `using UnityEditor;` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지 (반드시 `["Editor"]` 지정)

### Editor asmdef 구성

Editor-only asmdef 필수 필드:
- `name`: `Devian.Samples.<SampleName>.Editor`
- `rootNamespace`: `Devian`
- `references`: 최소 `["Devian.Samples.<SampleName>"]`
- `includePlatforms`: `["Editor"]`
- `noEngineReferences`: `false`

### 에디터 메뉴 경로

`Devian/Samples/<SampleName>/How to Use`

---

## Sample 네이밍 컨벤션 (동적 파생, 하드코딩 금지)

| 카테고리 | Suffix | SampleName 규칙 | Assembly 규칙 |
|----------|--------|----------------|---------------|
| Domain | `Package` | `{DomainKey}Package` | `Devian.Samples.{DomainKey}Package` |
| Protocol | `Protocol` | `{ProtocolGroup}Protocol` | `Devian.Samples.{ProtocolGroup}Protocol` |
| Manual | `Package` | `{Key}Package` | `Devian.Samples.{Key}Package` |

- 빌더 구현: `DevianToolBuilder.getSampleName(key, suffix)` → `${key}${suffix}`
- 위치: `Samples~/{Key}{Suffix}/`

> 상세 네이밍 규칙: [Builder SSOT § Sample 폴더 네이밍 컨벤션](../../devian/80-tools/11-builder/03-ssot/SKILL.md)

---

## Hybrid Sample (Builder-generated + Manual addon)

일부 샘플은 수동 addon 코드와 빌더가 생성하는 Generated 코드를 함께 포함한다.

**Hybrid Sample 규칙 (Hard Rule):**
- 빌더는 `Runtime/Generated/`와 `Editor/Generated/` 디렉토리만 clean+copy한다.
- 수동 addon 코드는 빌더가 touch하지 않는다 (보존).
- asmdef, package.json(상위 com.devian.samples의), README.md는 수동 관리 (빌더 생성/수정 금지).
- Generated 코드의 C# namespace는 원래 namespace를 유지:
  - Domain: `Devian.Domain.{DomainKey}`
  - Protocol: `Devian.Protocol.{ProtocolGroup}`
- assembly 이름은 `Devian.Samples.{SampleName}` 규약을 따른다.

---

## 빌드 통합 (Build Integration)

### Builder MUST copy Samples~ (Hard Rule)

Builder는 **반드시** `Samples~` 폴더를 upm에서 UnityExample/Packages로 복사해야 한다.

- Source에 `Samples~`가 존재하면 Target에도 **반드시** 존재해야 함
- `copyUpmToTarget()` 함수에서 `Samples~` 복사가 `syncSamplesMetadata()` 호출 **전에** 실행되어야 함

### samples metadata sync

`Samples~` 폴더를 제공하는 패키지는 `package.json`에 `samples` 배열을 선언해야 한다.
Builder는 `Samples~`가 존재하고 하위 폴더가 있으면 `samples` 필드를 자동 동기화한다.

**samples 항목 필수 필드:**

| 필드 | 필수 | 설명 |
|------|------|------|
| `displayName` | **필수** | Package Manager UI에 표시될 샘플 이름 |
| `description` | 권장 | 샘플 설명 |
| `path` | **필수** | `Samples~/...` 상대경로 (폴더명과 대소문자 정확히 일치) |

### samplePackages 설정

`{projectConfigJson}`에 샘플 패키지를 등록한다.

**samplePackages 규칙 (Hard Rule):**
- **반드시 문자열 배열**로 정의
- `com.devian.samples`만 허용 — 라이브러리/도메인 패키지는 포함 금지
- `staticUpmPackages` 키는 금지이며 사용 시 빌드 FAIL

---

## Disconnect 행동 DoD (Hard DoD)

**Disconnect 후 상태 갱신 필수:**

1. Disconnect 호출 시 **1초 이내**에 `OnClose` 이벤트가 발생해야 한다.
2. `IsConnected`가 `false`로 바뀌어야 한다.

**Hard FAIL 조건:**
- OnClose 없이 IsConnected만 false로 우회하면 **FAIL**
- 1초 후에도 OnClose가 발생하지 않으면 **FAIL**
- Disconnect는 Close 이벤트를 통해 상태가 갱신되어야 하며, Close 이전에 OnClose 핸들러를 제거하면 **FAIL**

### Packages 반영 확인 (Hard Rule)

- `upm`와 `Packages`의 파일이 다르면 **FAIL** (sync 누락)
- `Packages/`에서 직접 수정한 경우 **정책 위반**

---

## DoD (Definition of Done)

**Hard (반드시 0 위반)**
- 샘플 원본이 `framework-cs/upm/**/Samples~/**`에 존재
- `package.json.samples[]`에 등록되어 UPM Samples UI에서 노출
- UnityExample `Packages/**` 미러가 생성/동기화되어 일치
- Runtime/Editor 경계 위반 없음 (Runtime에서 UnityEditor 사용 금지)

---

## 금지

- `author.name`을 임의로 변경 금지
- 패키지마다 `unity` 최소버전이 달라지게 만들지 말 것
- `com.devian.*` 외 패키지의 package.json은 수정 금지
- 의존 방향 위반 금지 ([02-unity-bundles](../02-unity-bundles/SKILL.md) §의존 방향 정책 참조)
- `upm` 외부에서 샘플 소스 작성 금지
- `UnityExample/Packages/**` 직접 수정 금지 (빌드 출력물)
- Runtime 코드에 `using UnityEditor` 사용 금지
- Editor asmdef에 `includePlatforms: []` 사용 금지
- Close 처리에서 이벤트 unhook을 Close 이전에 수행 금지
- **GameNetwork 샘플 삭제됨** — Networker/SessionHost는 `Samples~/GameProtocol/` 샘플에서 generated code로 제공

---

## Reference

- Related: `skills/devian-unity/02-unity-bundles/SKILL.md`
- Related: `skills/devian/20-domain-common/02-module-policy/SKILL.md`
- Related: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian/80-tools/11-builder/03-ssot/SKILL.md` (Builder SSOT — Sample naming)
- UPM 소스: `framework-cs/upm/com.devian.foundation/Runtime/Module/Net/`
