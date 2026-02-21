# devian-unity/50-mobile-system — Policy

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian/10-module/03-ssot/SKILL.md

---

## 1. Templates 정의

**Templates**는 `com.devian.samples` UPM 패키지의 **Samples~** 폴더에 포함된 샘플 코드다.

- Unity Package Manager에서 "Import" 버튼으로 설치
- 설치 후 `Assets/Samples/`로 복사되어 프로젝트 소유가 됨
- 자유롭게 수정/삭제/확장 가능

---

## 2. 핵심 원칙

| 원칙 | 설명 |
|------|------|
| **단일 패키지** | 모든 템플릿은 `com.devian.samples` 패키지에 포함 |
| **Samples~ 방식** | Unity의 표준 샘플 제공 방식 사용 |
| **프로젝트 소유** | 설치 후 Assets/Samples/에 복사되어 프로젝트가 소유 |
| **충돌 방지** | `Devian` + `.Network.*`, `Devian` + `.Protocol.*`, `Devian` + `.Domain.*` 네임스페이스 사용 금지 |

정본/미러 동기화 하드룰은 상위 정책 `skills/devian-unity/01-policy/SKILL.md`를 따른다.

---

## 3. 네이밍 규칙

### 3.1 UPM 패키지명

```
com.devian.samples
```

단일 패키지에 모든 템플릿이 Samples~로 포함됨.

### 3.2 샘플 폴더명

```
Samples~/<TemplateName>/
```

예시:
- `Samples~/MobileSystem/`

### 3.3 asmdef 이름 (어셈블리명)

```
Devian.Samples.<TemplateName>
Devian.Samples.<TemplateName>.Editor
```

예시:
- `Devian.Samples.MobileSystem`
- `Devian.Samples.MobileSystem.Editor`

> **주의**: 위는 asmdef의 `name`(어셈블리명)이다. 코드의 namespace와 혼동하지 않는다.

### 3.4 namespace (Hard Rule)

**모든 샘플 코드는 단일 네임스페이스 `Devian`을 사용한다.**

```csharp
namespace Devian
```

> asmdef의 `rootNamespace`도 `"Devian"`으로 설정한다.

### 3.5 금지 프리픽스 (네임스페이스)

샘플 코드에서 다음 네임스페이스 사용 금지:

- `Devian.Network.*`
- `Devian.Protocol.*`
- `Devian.Domain.*`
- `Devian.Core.*`
- `Devian.Templates.*` (샘플 코드 namespace로 사용 금지)
- `Devian.Samples.*` (샘플 코드 namespace로 사용 금지 — asmdef name으로만 사용)

> 어셈블리명(asmdef name)은 `Devian.Samples.*`를 사용하지만, namespace는 `Devian` 단일만 사용한다.

---

## 4. 경로 정책

### 4.1 Template 원본 (upm)

```
framework-cs/upm/com.devian.samples/
├── package.json
├── Samples~/
│   ├── MobileSystem/
│   │   ├── Runtime/
│   │   │   └── [asmdef: Devian.Samples.MobileSystem]
│   │   ├── Editor/
│   │   │   └── [asmdef: Devian.Samples.MobileSystem.Editor]
│   │   └── README.md
│   └── (다른 템플릿들...)
└── README.md
```

### 4.2 빌드 출력 (packageDir)

```
{upmConfig.packageDir}/com.devian.samples/
```

### 4.3 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Templates/{version}/{TemplateName}/
```

---

## 5. FAIL 조건

- [ ] 샘플 패키지에 `Devian.Network.*`/`Devian.Protocol.*` namespace 코드가 존재
- [ ] package.json name이 `com.devian.samples`가 아님
- [ ] Samples~ 폴더가 없거나 비어있음
- [ ] samples 메타데이터가 package.json에 없음

---

## Related

- [07-samples-creation-guide](../../07-samples-creation-guide/SKILL.md)
- [Root SSOT](../../../devian/10-module/03-ssot/SKILL.md)
- [devian-unity/01-policy](../../01-policy/SKILL.md)
