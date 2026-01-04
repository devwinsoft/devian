# Devian v10 — Common Standard Types (Reserved)

Status: DRAFT  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

프로젝트에서 재사용할 수 있는 “표준 공용 타입”을 Common 도메인에 정리하기 위한 규칙을 제안한다.

⚠️ v10-wireframe 코드 기준으로는 별도의 표준 타입 패키지(예: `GFloat3`)가 **기본 제공되지 않는다**.
따라서 이 문서는 **예약(Reserved) 정책**이며, 실제 도입 전에는 코드/Reference가 먼저 추가되어야 한다.

---

## Proposed Rules

1) 공용 값 타입을 추가한다면 DomainKey=`Common` contracts에 정의한다.
2) 타입명 prefix 규칙을 쓴다면 `G*`를 사용한다.
3) 외부 플랫폼(Unity/Web) 의존은 Common 안에 두지 않는다.

---

## Adoption Gate

이 문서를 ACTIVE로 올리려면:

- `docs/generated/devian-reference.md`에 표준 타입 목록이 나타나야 한다(코드 기반).
- build.json 및 생성물 구조가 이를 실제로 반영해야 한다.

---

## Reference

- Common policy: `skills/devian-common/70-common-domain-policy/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`