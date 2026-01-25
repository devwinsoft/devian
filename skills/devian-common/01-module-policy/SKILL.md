# Devian v10 — Common Module Policy

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Common 모듈(C# 프로젝트명: `Devian` + `.Module.Common`, TS `@devian/module-common`)의 구조와 정책을 정의한다.

이 문서는 "Common = Domain + Module" 혼용을 인정하되, **생성물/수동 코드 경계**와 **features 구조**를 정책으로 분리한다.

---

## Hard Rules (MUST)

### 1. Common 필수

Devian v10 프로젝트는 DATA DomainKey로 `Common`을 **반드시** 포함한다.

- `input/input_common.json`에서 `domains.Common`은 필수 항목이다.
- 결과로 Common 모듈(C#/TS)은 항상 생성/유지된다:
  - C#: `Devian` + `.Module.Common` (프로젝트명)
  - TS: `@devian/module-common` (폴더명: `devian-domain-common`)

### 1.1 Common은 모든 생성 산출물의 기본 의존성 (Hard Rule)

Common 모듈은 Devian v10에서 생성되는 **모든 Module/DATA 도메인 모듈과 Protocol 모듈이 무조건 참조해야 하는 기반 모듈**이다.

- 예외: Common 모듈 자기 자신은 자기 자신을 참조하지 않는다.
- “참조 판정”은 하지 않는다. 항상 참조한다.

필수 적용:

- DATA Domain 모듈(프로젝트명: `Devian` + `.Module.{DomainKey}`, `@devian/module-{domainkey}`)
  - `{DomainKey} != Common`이면 반드시 `Devian` + `.Module.Common` 프로젝트 / `@devian/module-common`을 참조한다.
- PROTOCOL 모듈(`Devian.Protocol.{ProtocolGroup}`, `@devian/network-{protocolgroup}`)
  - 모든 ProtocolGroup은 반드시 `Devian` + `.Module.Common` 프로젝트 / `@devian/module-common`을 참조한다.

참조 방식(정책):

- C#: `.csproj`에 `Devian` + `.Module.Common` ProjectReference 포함 (프로젝트 참조, 네임스페이스 아님)
- C# PROTOCOL 생성물(`*.g.cs`): `using Devian;` 포함 (namespace는 Devian 단일)
- TS: `package.json` `dependencies`에 `@devian/module-common` 포함

### 2. 생성물/수동 코드 경계

| 영역 | 소유자 | 설명 |
|------|--------|------|
| `Generated/` | 빌더 | 항상 clean+copy 대상. 수동 편집 금지. |
| `features/` | 개발자 | 공용 기능(crypto 등) 수동 코드 영역. |

**경로:**

- C#: `Devian` + `.Module.Common` + `/Features/**`
- TS: `devian-domain-common/features/**`

### 2.1 C# Namespace 규칙 (Hard Rule)

Common 모듈의 C# 코드는 **반드시** 단일 네임스페이스를 사용한다.

- 모듈 루트: `namespace Devian`
- features 코드도 동일: `namespace Devian`

> `features/` 폴더는 **물리적 정리용 폴더**일 뿐이며, **namespace 분리 근거가 아니다**.
> 프로젝트 이름(`Devian` + `.Module.Common`)과 namespace(`Devian`)는 별개다.

**금지 (MUST NOT):**
- ❌ `namespace Devian` + `.<X>` (X ≠ Domain, Protocol) - 어떤 하위 네임스페이스도 금지
- ❌ `using Devian` + `.<X>.*;` (X ≠ Domain, Protocol)

**허용 (예외):**
- ✅ `namespace Devian.Domain.{DomainKey}` (Domain 생성물)
- ✅ `namespace Devian.Protocol.{ProtocolGroup}` (Protocol 생성물)

**재발 방지:**
- C# 코드에서 `namespace Devian` + `.<X>` (X ≠ Domain, Protocol) 문자열이 발견되면 **빌드 실패**
- 빌더(`build.js`)에서 검사하여 발견 시 즉시 throw

> Common(DATA) 생성물(`Generated/Common.g.cs`)은 `namespace Devian.Domain.Common` 아래에 생성된다.

### 2.2 .NET 타겟 버전 (Hard Rule)

| 타겟 | 버전 | 비고 |
|------|------|------|
| Unity | .NET Standard 2.1 | Unity 2021.3+ 기본값 |
| 서버/CLI | .NET 8 | LTS |

- Unity 타겟 코드(UPM 패키지)는 **.NET Standard 2.1** 호환 API만 사용
- .NET Standard 2.1에 없는 API 사용 시 Unity에서 컴파일 에러 발생
- 컴파일 에러 발생 시 해당 API의 대안을 찾아 수정

### 3. TS index 자동 관리 (Marker 방식)

TS `devian-domain-*/index.ts`는 빌더가 관리한다.

**통째 덮어쓰기 금지** — marker 구간만 갱신한다.

#### Marker 규격

```typescript
// <devian:domain-exports>
// ... 빌더가 자동 생성 ...
// </devian:domain-exports>

// <devian:feature-exports>
// ... 빌더가 자동 생성 ...
// </devian:feature-exports>
```

#### 규칙

- 빌더는 marker 구간만 교체하며, 나머지 영역은 보존한다.
- 개발자는 marker 안을 **절대 수정하지 않는다**.
- marker 밖도 수정할 필요 없음 — 기능 추가는 `features/`에만 한다.

### 4. features/index.ts 자동 관리

`features/index.ts`도 빌더가 marker 구간을 자동 갱신한다.

#### 스캔 규칙

빌더는 `features/` 직하의 항목만 스캔한다.

**제외:**
- `index.ts`
- `*.test.ts`, `*.spec.ts`
- `.d.ts`
- 숨김 파일 (`.`로 시작)
- `Generated` 폴더

**포함:**
- `*.ts` 파일 → `export * from './{basename}';`
- 디렉토리 → `export * from './{dir}';` (해당 디렉토리의 index.ts가 책임)

**정렬:**
- 항목명을 `localeCompare`로 정렬 (결정적)

---

## Soft Rules (SHOULD)

### features 네이밍 규칙

- 파일: `{feature}.ts` (예: `crypto.ts`, `time.ts`)
- 폴더: `{feature}/index.ts` (예: `crypto/index.ts`)
- 스캔 가능하고 결정적이어야 함

### "Common Dumping Ground" 방지 기준

Common에 기능을 추가할 때 다음을 만족해야 한다:

1. **범용성**: 여러 모듈에서 재사용되는가?
2. **결합도**: 다른 모듈과의 결합도를 증가시키지 않는가?
3. **테스트 용이성**: 테스트 가능하고 교체 가능한가?
4. **비종속성**: 특정 서비스/도메인에 종속되지 않는가?

위 기준을 만족하지 않으면 Common이 아닌 해당 도메인/모듈에 둔다.

---

## Directory Structure

### C#

```
# 프로젝트 디렉토리: Devian + .Module.Common
├── Generated/
│   └── Common.g.cs      # 빌더 소유 (수정 금지)
└── Features/
    └── Crypto.cs        # 개발자 소유 (수동 코드)
```

### TypeScript

```
devian-domain-common/
├── Generated/
│   └── Common.g.ts      # 빌더 소유 (수정 금지)
├── features/
│   ├── index.ts         # 빌더 관리 (marker 갱신)
│   └── crypto.ts        # 개발자 소유 (수동 코드)
├── index.ts             # 빌더 관리 (marker 갱신)
├── package.json
└── tsconfig.json
```

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Domain Policy: `skills/devian-common/00-domain-policy/SKILL.md`
- Feature Skills: `skills/devian-common/10-feature-*/SKILL.md`
