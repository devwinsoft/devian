# Devian v10 — ProtocolGen Implementation Notes (Policy View)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Protocol generator의 구현을 “문서로 재서술”하지 않기 위한 가드 문서다.

이 스킬은 **정책적으로 필요한 검증 포인트만** 남기고,
구체적인 구현/산출 API는 모두 **런타임/제너레이터 코드**로 위임한다.

---

## Implementation is Code Truth

아래 항목은 SKILL이 아니라 **코드/Reference가 정답**이다.

- 생성되는 C#/TS 코드 구조, 클래스/타입 이름
- codec의 정확한 인코딩/디코딩 구현
- 프레임 포맷(바이트 레이아웃)
- “sender / transport” 계층 분리 방식과 인터페이스 시그니처

SKILL은 위 내용을 단정해서는 안 된다.

---

## Policy-level Checks (MUST)

1) ProtocolSpec 파일은 JSON이다.
2) opcode/tag는 결정적이어야 한다.
3) Tag reserved range(19000..19999)는 금지다.
4) Registry 파일(opcodes/tags)은 키 정렬 등으로 **결정적으로 저장**되어야 한다.
5) 생성되는 모든 PROTOCOL 모듈은 Common 모듈을 **무조건 참조**해야 한다.
   - Common 참조 판정은 하지 않는다.
   - C#: csproj ProjectReference + `*.g.cs`에 `using Devian.Module.Common;`
   - TS: package.json dependencies에 `@devian/module-common`

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드