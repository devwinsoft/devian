# Devian v10 — Generated Integration Rules

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

generated 산출물을 프로젝트에 통합할 때의 **소유권/폴더/수정 금지 규칙**을 정의한다.

이 문서는 “generated를 어떻게 취급할지”만 말한다.
실제 생성 파일 목록은 **`docs/generated/devian-reference.md`**를 참조한다.

---

## Ownership

- `modules/**/generated/**` 는 **기계 소유**다.
- 사람은 이 파일을 수정하지 않는다.
- 수정이 필요하면 입력(contracts/tables/protocols) 또는 generator 코드 변경으로 해결한다.

## Commit Policy

- generated는 **커밋 대상**이다.
  - 빌드 없이도 소비자가 타입/codec을 사용할 수 있어야 한다.

## Directory Expectations

정확한 출력 루트는 build.json의 targetDirs가 정본이다.

권장(설명용) 구조:

```
modules/
├── cs/{DomainKey or LinkName}/generated/**
└── ts/{DomainKey or LinkName}/generated/**
data/
└── {DomainKey}/json/**.ndjson
```

> 실제 폴더명/레이아웃은 프로젝트 구성에 따라 달라질 수 있으며, Reference가 정답이다.

---

## Must / Must Not

MUST

- generated를 import하는 “수동 코드(manual)”는 generated와 분리된 폴더에서 관리한다.
- build.json targetDirs 설계로 산출 충돌을 방지한다.

MUST NOT

- generated 파일을 직접 패치해서 ‘임시로’ 문제를 해결하지 않는다.

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`