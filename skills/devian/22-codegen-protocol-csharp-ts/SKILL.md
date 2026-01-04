# Devian v10 — Protocol Codegen (C# / TS)

Status: ACTIVE  
AppliesTo: v10  
SSOT: skills/devian/03-ssot/SKILL.md

## Purpose

Protocol codegen의 언어별 산출물(C#/TS)에 대한 **문서 정책**을 정의한다.

이 문서는 “언어별로 무엇을 생성해야 한다”를 **추상적으로**만 서술한다.
정확한 클래스/함수/시그니처/프레임은 **`docs/generated/devian-reference.md`**를 정답으로 본다.

---

## C# Output Policy

- ProtocolName 단위로 하나의 C# 생성물 파일을 만든다.
- 생성물은 “메시지 정의 + codec + 소비자 구현 지점(handlers/stub 등) + 발신 프록시(sender/proxy 등)”을 제공해야 한다.
- C# 생성물은 **Unity 호환(IL2CPP/Span 제한 고려)** 을 전제로 한다.

## TypeScript Output Policy

- ProtocolName 단위로 하나의 TS 생성물 파일을 만든다.
- TS 생성물은 “메시지 타입 + codec + 소비자 구현 지점 + 발신 프록시”를 제공해야 한다.

## Shared Policy

- opcode/tag 할당은 Registry 정책(SSOT)을 따른다.
- JSON codec는 디버깅/툴링 목적을 지원한다.
- Protobuf-style codec는 런타임 성능 목적을 지원한다.

---

## Reference

- Overview: `skills/devian/20-codegen-protocol/SKILL.md`
- Policy SSOT: `skills/devian/03-ssot/SKILL.md`
- Code-based Reference: `docs/generated/devian-reference.md`