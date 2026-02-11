# 03-ssot — Tools

Status: ACTIVE
AppliesTo: v10
ParentSSOT: skills/devian-core/03-ssot/SKILL.md

---

## Scope

이 문서는 **빌드 파이프라인, 검증, 워크스페이스** 관련 SSOT를 정의한다.

**중복 금지:** 공통 용어/플레이스홀더/입력 분리/머지 규칙은 [Root SSOT](../../devian-core/03-ssot/SKILL.md)가 정본이며, 이 문서는 재정의하지 않는다.

---

## Phase 모델

빌드는 **4단계**로 해석한다.

1. **Generate**: 모든 산출물은 **staging({tempDir})에만 생성**
2. **Materialize**: staging → module/upm/ts-modules로 **clean + copy** (모듈/패키지 단위)
3. **Validate**: module/module과 upm의 **완결성 검증**
4. **Sync**: upm → packageDir로 **패키지 단위 동기화**

> staging({tempDir}) 외의 위치에 직접 생성하는 동작은 금지한다.

---

## Generated Only 정책 (Hard Rule)

**생성기(clean+generate)가 건드리는 영역은 오직 폴더명이 `Generated`인 디렉토리만 허용한다.**

| 영역 | 허용 경로 | 금지 |
|------|-----------|------|
| staging | `{tempDir}/{DomainKey}/cs/Generated/**` | `generated`, `_Shared`, `Templates` 등 |
| staging | `{tempDir}/{DomainKey}/ts/Generated/**` | 동일 |
| final (CS) | `{csConfig.generateDir}/.../Generated/**` | 동일 |
| final (TS) | `{tsConfig.generateDir}/.../Generated/**` | 동일 |
| UPM | `Runtime/Generated/**`, `Editor/Generated/**` | `Runtime/Generated`, `Runtime/_Shared` 등 |

**고정 유틸(수기 코드) 영역:**

`com.devian.foundation`의 아래 폴더는 수기 코드로 유지하며, 생성기가 절대 clean/generate하지 않는다:
- `Runtime/Unity/_Shared/` — UnityMainThread, UnityMainThreadDispatcher
- `Runtime/Unity/Singletons/` — Singleton, SingletonRegistry, AutoSingleton, CompoSingleton
- `Runtime/Unity/Pool/` — IPoolable, IPoolFactory, PoolManager, Pool
- `Runtime/Unity/PoolFactories/` — InspectorPoolFactory, BundlePoolFactory
- `Runtime/Unity/AssetManager/` — AssetManager, DownloadManager (bootstrap/download utilities)
- `Runtime/Module/` — 순수 C# 코드 (UnityEngine 의존 없음)

**레거시 경로 cleanup:**
- 빌더는 기존 `generated`(소문자) 폴더가 존재하면 자동 제거
- 마이그레이션 완료 후 레포에 `generated` 폴더가 0개여야 함

---

## TypeScript Workspace 정본 (Hard Rule)

- TS 의존성 설치는 `framework-ts/` 루트에서만 수행한다. (단일 `node_modules`)
- workspace root는 `framework-ts/package.json` 단 하나만 허용한다.
- lockfile은 `framework-ts/package-lock.json` 단 하나만 허용한다.

자세한 규칙: [skills/devian/20-workspace](../../devian/20-workspace/SKILL.md)

---

## tsConfig 설정 (v10)

TS 산출물의 반영 위치를 관리하는 설정:

```json
"tsConfig": {
  "moduleDir": "../framework-ts/module"
}
```

| 필드 | 역할 | 필수 | 예시 |
|------|------|------|------|
| `moduleDir` | TS 모듈 루트 | ✅ | `../framework-ts/module` |
| `generateDir` | 생성 TS 모듈 루트 (선택) | ❌ | `../framework-ts/module-gen` |

**통합 모드 (기본):**
- `generateDir`를 생략하면 `moduleDir`을 생성 출력 루트로 사용
- 빌더는 `moduleDir` 하위에 staging 결과를 **clean+copy**

**분리 모드 (선택):**
- `generateDir`를 `moduleDir`와 다른 경로로 명시하면 분리 모드
- 이 경우 `moduleDir`은 검증만, `generateDir`에만 clean+copy

---

## Validate 단계 (Hard Rule)

**C# 모듈 검증:**
- `module` (수동): 각 모듈에 `.csproj` 존재, "완벽한 C# 모듈"
- `module` (생성): 각 모듈에 `.csproj` 존재, "완벽한 C# 모듈"

**UPM 패키지 검증:**
- `upm` (수동): 각 패키지에 `package.json` 존재, 폴더명 == `package.json.name`
- `upm` (생성): 각 패키지에 `package.json` 존재, 폴더명 == `package.json.name`

> "완벽한 C# 모듈" = `.csproj` 존재 + ProjectReference 경로 유효 + dotnet build 가능

---

## tempDir 경로 해석 및 분리 (Hard Rule)

**`{tempDir}`는 실행한 입력 설정 JSON이 위치한 디렉토리 기준 상대경로로 해석된다.**

빌더는 실행 시 `{tempDir}`를 **clean(rm -rf) 후 재생성**한다. 따라서:

- **동일 repo에서 서로 다른 입력 설정 JSON을 번갈아 실행하는 경우, tempDir을 공유하면 서로 staging을 삭제할 수 있으므로 tempDir 분리는 필수다.**

| 설정 파일 | tempDir 권장값 |
|----------|---------------|
| `{buildInputJson}` (예: `input/input_common.json`) | `"temp"` |
| 대체 입력 (예시) | `"temp_alt"` |

---

## Clean + Copy 정책

- Copy 단계는 targetDir을 **clean 후 copy**한다.
- **충돌 방지 책임은 `{buildInputJson}` 설계(타겟 분리)에 있다.**
  - 동일 targetDir에 여러 DomainKey/ProtocolGroup을 매핑하면, clean 단계 때문에 서로 덮어쓰거나 삭제될 수 있다.

---

## 디렉토리 역할 정의

| 디렉토리 | 역할 | 빌드 동작 |
|----------|------|-----------|
| `framework-cs/module` | 수동/생성 C# 모듈 | staging 결과로 생성/반영 |
| `framework-ts/module` | 수동/생성 TS 모듈 | staging 결과로 생성/반영 |
| `framework-cs/upm` | 수동/생성 UPM 패키지 | staging 결과로 생성/반영 |
| `framework-cs/apps/UnityExample/Packages` | Unity 최종 패키지 | upm → sync |

---

## 공식 빌드 러너

공식 빌드 실행은 Node 기반 빌더를 사용한다: `framework-ts/tools/builder/build.js`

실행 예시: `npm run builder -- ../{buildInputJson}`

### 빌드 입력/설정/산출물

도메인/프로토콜 동적 빌드 정책 (입력 스펙, 설정 스펙, 산출물 규칙):

> **정책 문서:** [skills/devian-tools/20-build-domain](../20-build-domain/SKILL.md)

### 빌드 에러 리포팅

빌드 실패 시 에러 출력 규격 및 로그 파일 규칙:

> **정책 문서:** [skills/devian-tools/21-build-error-reporting](../21-build-error-reporting/SKILL.md)

---

## See Also

- [Root SSOT](../../devian-core/03-ssot/SKILL.md) — 공통 용어/플레이스홀더/머지 규칙
- [Tools Policy](../01-policy/SKILL.md)
- [Workspace](../../devian/20-workspace/SKILL.md)
