# Devian v10 — Receiver Skeleton / Routing (Policy)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

수신 처리의 기본 골격(스켈레톤)을 정의한다.

이 문서는 “라우팅이 opcode 기반이어야 한다” 수준의 정책만 제공한다.
구체적인 handler/stub/dispatcher 형태와 시그니처는 **런타임/제너레이터 코드**가 정답이다.

---

## Receiver Responsibilities

1) **프레임 파싱**
- 수신된 bytes에서 opcode를 추출
- payload를 추출

2) **opcode → 메시지 매핑**
- opcode에 대응하는 메시지 타입을 선택
- codec으로 decode

3) **유저 코드로 전달**
- domain/business 로직(유저 핸들러) 호출

---

## Must / Must Not

MUST

- opcode/tag 정책(SSOT)의 결정성을 위반하지 않는다
- Unknown opcode는 무시하지 말고 명확한 에러/로그 경로를 가진다

MUST NOT

- “자동으로 재배정” 같은 동작을 수신 런타임에서 수행하지 않는다

---

## Reference

- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Transport adapter: `skills/devian/40-ws-transport-adapter/SKILL.md`
- 동작 정본: 런타임/제너레이터 코드