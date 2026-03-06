# devian/20-domain-common — Policy (Domain Policy)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/10-module/03-ssot/SKILL.md

## Purpose

Common(DomainKey=`Common`) DATA 도메인의 사용 목적과 제약을 정의한다.

이 문서는 "Common을 DATA DomainKey로 어떻게 쓸 것인가"의 정책만 가진다.
Common 생성물의 구체 타입/파일은 **런타임/제너레이터 코드**가 정답이다.

> **Note**: Common 모듈 전체 정책(생성물/수동 코드 경계, features 구조 등)은 `02-module-policy`를 참조한다.

---

## What Common Is

- Common은 DATA 도메인 중 하나다.
- 프로젝트 전역에서 공유할 수 있는 contracts/tables를 담기 위한 관례적 도메인이다.
- Devian v10 프로젝트는 Common DomainKey를 **필수**로 포함한다.

---

## Hard Rules (MUST)

1) Common도 다른 DomainKey와 동일한 방식으로 `{buildInputJson}`에 정의한다.
   - `domains.Common.contractDir`, `domains.Common.tableDir` 등

2) v10 구현 기준으로, 타입 참조는 **동일 DomainKey 범위 내**에서만 안정적으로 동작한다고 가정한다.
   - `enum:Name`, `class:Name` 형태는 "현재 도메인에 존재하는 타입"을 전제로 한다.
   - **Cross-domain 참조(Common 타입을 다른 DomainKey에서 직접 참조)는 정책상 금지(또는 미지원)**로 취급한다.

---

## Soft Rules (SHOULD)

- 공용으로 쓰일 가능성이 높은 enum/class만 Common으로 이동한다.
- 게임/서비스 특화 타입은 해당 DomainKey에 둔다.

---

## Directory Structure

Common Domain 정책이 적용되는 디렉토리 정본은 다음과 같다.

### Input (Source)

- Contracts: `input/Domains/Common/*.json`
- Tables: `input/Domains/Common/*.xlsx`

### Build Staging

- `{tempDir}/Common/**`

### Generated Outputs

- C# 모듈 생성물: `framework-cs/module/Devian.Domain.Common/Generated/Common.g.cs`
- C# 모듈 수동 코드: `framework-cs/module/Devian.Domain.Common/src/CommonError.cs`, `framework-cs/module/Devian.Domain.Common/src/CommonResult.cs`
- TS 모듈 생성물: `framework-ts/module/devian-domain-common/Generated/Common.g.ts`
- TS 모듈 엔트리/feature export: `framework-ts/module/devian-domain-common/features/index.ts`, `framework-ts/module/devian-domain-common/index.ts`
- Unity UPM 생성 미러: `framework-cs/upm/com.devian.domain.common/Runtime/Generated/**`

### Final Data Outputs

- SSOT의 `tableConfig.tableDirs/stringDirs/soundDirs`가 정본이다.
- `domains.Common.*TargetDirs` 같은 per-domain target 키는 금지한다.

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- Module Policy: `skills/devian/20-domain-common/02-module-policy/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
