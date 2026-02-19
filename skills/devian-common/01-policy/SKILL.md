# devian-common — Policy (Domain Policy)

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

## Paths

Common도 SSOT의 DATA 경로 규약을 따른다.

- staging: `{tempDir}/Common/**`
- final: SSOT의 `tableConfig.tableDirs/stringDirs/soundDirs`가 정본 (`domains.Common.*TargetDirs` 같은 per-domain target은 금지)

---

## Reference

- Policy SSOT: `skills/devian/10-module/03-ssot/SKILL.md`
- Module Policy: `skills/devian-common/02-module-policy/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
