# Devian v10 — Skill Specification

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Devian 문서(SKILL)의 **작성 규격**을 정의한다.

이 문서는 "SKILL이 무엇을 포함/제외해야 하는지"만 다룬다.
코드/생성물/시그니처/프레임 포맷 같은 구현 세부는 **런타임/제너레이터 코드**를 정답으로 본다.

---

## 1. SKILL 문서가 포함해야 하는 것

SKILL은 반드시 아래만 다룬다.

1) **정책(Policy)**
- 무엇이 정본인지(SSOT)
- 금지/필수 규칙

2) **입력 규약(Input contract)**
- 입력 파일 포맷(예: JSON 스키마의 "필수/선택" 수준)
- 테이블 작성 규칙(헤더/옵션/중단 규칙)

3) **경로 규약(Path contract)**
- input_common.json 기준으로 staging/final 경로를 어떻게 해석하는지
- 플레이스홀더 표준

4) **검증 규칙(Validation)**
- 빌드 실패 조건(FAIL fast)
- Hard/Soft conflict 분류 기준

---

## 2. SKILL 문서에 쓰면 안 되는 것

아래는 **SKILL에서 금지**한다(코드가 정본).

- 생성되는 **클래스/인터페이스/함수 시그니처**
- 프레임 바이너리 레이아웃의 바이트 단위 정의
- 런타임 패키지의 파일 목록/구현 디테일
- "예시 코드"가 실제 코드 API와 동기화되지 않을 가능성이 있는 내용

예외: **입력 규약을 설명하는 최소한의 짧은 예시(JSON 한 조각, 테이블 한 셀 값 등)**은 허용한다.

---

## 3. 용어/플레이스홀더 강제

SSOT의 용어/플레이스홀더 표준을 그대로 따른다.

- 용어: DomainType / DomainKey / ProtocolGroup / ProtocolName
- 플레이스홀더: `{tempDir}`, `{DomainKey}`, `{ProtocolGroup}`, `{ProtocolName}`, `{csConfig.moduleDir}`, `{csConfig.generateDir}`, `{tsConfig.moduleDir}`, `{tsConfig.generateDir}`, `{dataConfig.bundleDirs}`, `{upmConfig.sourceDir}`, `{upmConfig.packageDir}`

> `{dataConfig.bundleDirs}`는 배열이다. 문서에서 배열 내 개별 요소를 지칭할 때 `{bundleDir}`로 표기할 수 있다.

**금지 플레이스홀더:**
- `{csTargetDir}`, `{tsTargetDir}`, `{dataTargetDirs}`, `{dataTargetDir}`, `{upmTargetDir}` — deprecated
- `{dataConfig.tableDirs}` — deprecated, `{dataConfig.bundleDirs}` 사용

**"domain", "name", "{domain}", "{name}" 단독 사용 금지.**

---

## 4. Hard Conflicts / Soft Conflicts 표기 규칙

SKILL 문서가 충돌을 다룰 때는 다음을 따른다.

- Hard Conflicts: 빌드/호환성/입력 규약이 깨지는 수준 → **즉시 FAIL**
- Soft Conflicts: 용어/표기/톤/링크 → **정리 대상(충돌 아님)**

Soft를 이유로 "충돌 0개 만들기" 무한 루프를 돌리지 않는다.

---

## 5. 문서 헤더 표준

모든 SKILL은 상단에 아래 메타를 가진다.

- `Status: ACTIVE | DRAFT`
- `AppliesTo: v10`
- `SSOT: skills/devian/03-ssot/SKILL.md`

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드
