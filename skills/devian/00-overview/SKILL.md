# Devian v10 — Overview

Status: ACTIVE
AppliesTo: v10
SSOT: skills/devian-core/03-ssot/SKILL.md

## What is Devian?

Devian은 **DATA**와 **PROTOCOL** 두 축으로 구성된 코드 생성 프레임워크다.

| 축 | 역할 | 입력 | 출력 |
|-----|------|------|------|
| **DATA** | 테이블/계약 타입 생성 | XLSX, JSON contracts | C#/TS Entity, Container, NDJSON/pb64 |
| **PROTOCOL** | 네트워크 메시지 생성 | Protocol JSON | C#/TS Message, Codec, Stub |

---

## Policy SSOT

모든 정책/경로/규칙의 정본은 **SSOT 문서**에 있다:

→ `skills/devian-core/03-ssot/SKILL.md`

이 문서를 포함한 모든 스킬은 SSOT를 참조하며, 충돌 시 SSOT가 우선한다.

---

## Devian Skill Groups

| Group | Description | Overview | Policy | SSOT |
|-------|-------------|----------|--------|------|
| **devian-core** | Root SSOT, 스킬 규격, 런타임 | [00-overview](../devian-core/00-overview/SKILL.md) | [01-policy](../devian-core/01-policy/SKILL.md) | [03-ssot](../devian-core/03-ssot/SKILL.md) |
| **devian-tools** | Builder, CLI, 아카이브 | [00-overview](../devian-tools/00-overview/SKILL.md) | [01-policy](../devian-tools/01-policy/SKILL.md) | [03-ssot](../devian-tools/03-ssot/SKILL.md) |
| **devian-data** | Table, Contract, NDJSON, PB64 | [00-overview](../devian-data/00-overview/SKILL.md) | [01-policy](../devian-data/01-policy/SKILL.md) | [03-ssot](../devian-data/03-ssot/SKILL.md) |
| **devian-protocol** | Protocol codegen, Opcode, Registry | [00-overview](../devian-protocol/00-overview/SKILL.md) | [01-policy](../devian-protocol/01-policy/SKILL.md) | [03-ssot](../devian-protocol/03-ssot/SKILL.md) |
| **devian-common** | Common 도메인, Feature 모듈 | [00-overview](../devian-common/00-overview/SKILL.md) | [01-policy](../devian-common/01-policy/SKILL.md) | — |
| **devian-unity** | UPM 패키지, Unity 런타임, 컴포넌트 | [00-overview](../devian-unity/00-overview/SKILL.md) | [01-policy](../devian-unity/01-policy/SKILL.md) | [03-ssot](../devian-unity/03-ssot/SKILL.md) |
| **devian-examples** | 예제 도메인, 예제 프로토콜 | [00-overview](../devian-examples/00-overview/SKILL.md) | [01-policy](../devian-examples/01-policy/SKILL.md) | — |
| **devian-unity-samples** | Unity 샘플 작성 가이드, 네트워크 샘플 | [00-overview](../devian-unity-samples/00-overview/SKILL.md) | [01-policy](../devian-unity-samples/01-policy/SKILL.md) | — |

---

## Navigation

| 찾고 싶은 것 | 문서 |
|-------------|------|
| 용어 정의 | [10-glossary](./10-glossary/SKILL.md) |
| Workspace 구조 | [20-workspace](./20-workspace/SKILL.md) |
| 공통 정책 | [01-policy](./01-policy/SKILL.md) |
| 빌드 실행 | [devian-tools/20-build-domain](../devian-tools/20-build-domain/SKILL.md) |
| 테이블 작성 규칙 | [devian-data/30-table-authoring-rules](../devian-data/30-table-authoring-rules/SKILL.md) |
| 프로토콜 코드젠 | [devian-protocol/40-codegen-protocol](../devian-protocol/40-codegen-protocol/SKILL.md) |
| Unity 정책 | [devian-unity/01-policy](../devian-unity/01-policy/SKILL.md) |

---

## Reference

- Index: [skills/devian/SKILL.md](./SKILL.md)
- SSOT: [skills/devian-core/03-ssot/SKILL.md](../devian-core/03-ssot/SKILL.md)
