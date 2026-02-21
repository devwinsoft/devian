# Devian v10 — Index

Status: ACTIVE
AppliesTo: v10

## Start Here

| 문서 | 설명 |
|------|------|
| **SSOT** | [skills/devian/10-module/03-ssot](./10-module/03-ssot/SKILL.md) — 모든 정책/경로/규칙의 정본 |
| **Overview** | [skills/devian/00-overview](./00-overview/SKILL.md) — Devian이 무엇인지 |
| **Common Policy** | [skills/devian/01-policy](./01-policy/SKILL.md) — 프레임워크 공통 정책 |
| **Glossary** | [skills/devian/04-glossary](./04-glossary/SKILL.md) — 용어/플레이스홀더 정의 |
| **Workspace** | [skills/devian/05-workspace](./05-workspace/SKILL.md) — npm workspace 구조 |

---

## Inputs (Quick Reference)

| 플레이스홀더 | 설명 | 예시 |
|-------------|------|------|
| `{buildInputJson}` | 빌드 입력 JSON | `input/input_common.json` |
| `{projectConfigJson}` | 프로젝트 설정 JSON | `input/config.json` |

정확한 키/머지 규칙: [skills/devian/10-module/03-ssot](./10-module/03-ssot/SKILL.md)

---

## SSOT Hub

| Category | SSOT | 범위 |
|----------|------|------|
| **Root** | [devian/10-module/03-ssot](./10-module/03-ssot/SKILL.md) | 공통 용어, 플레이스홀더, 입력 분리, 머지 규칙 |
| **Tools** | [devian-tools/03-ssot](../devian-tools/03-ssot/SKILL.md) | 빌드 파이프라인, Phase, Validate, tempDir |
| **Builder** | [devian-tools/11-builder/03-ssot](../devian-tools/11-builder/03-ssot/SKILL.md) | tableConfig, Tables, NDJSON, pb64, Protocol Spec, Opcode/Tag, Protocol UPM |
| **Unity** | [devian-unity/03-ssot](../devian-unity/03-ssot/SKILL.md) | upmConfig, UPM Sync, Foundation |
| **Examples** | [devian-examples/03-ssot](../devian-examples/03-ssot/SKILL.md) | config/input JSON, TS apps, Unity Example |
| **Purchase System** | [devian-unity/50-mobile-system/30-purchase-system/03-ssot](../devian-unity/50-mobile-system/30-purchase-system/03-ssot/SKILL.md) | Unity IAP + 결제 검증(Functions) + 멱등/구독 상태(Firestore) |

---

## I want to…

| 목적 | 문서 |
|------|------|
| 빌드 실행하기 | [skills/devian-tools/11-builder/20-build-domain](../devian-tools/11-builder/20-build-domain/SKILL.md) |
| 빌드 에러 이해하기 | [skills/devian-tools/11-builder/21-build-error-reporting](../devian-tools/11-builder/21-build-error-reporting/SKILL.md) |
| 아카이브/배포하기 | [skills/devian-tools/90-project-archive](../devian-tools/90-project-archive/SKILL.md) |
| 테이블 작성하기 | [skills/devian-tools/11-builder/30-table-authoring-rules](../devian-tools/11-builder/30-table-authoring-rules/SKILL.md) |
| NDJSON/Row IO 이해하기 | [skills/devian-tools/11-builder/32-json-row-io](../devian-tools/11-builder/32-json-row-io/SKILL.md) |
| ContractGen 구현 보기 | [skills/devian-tools/11-builder/43-contractgen-implementation](../devian-tools/11-builder/43-contractgen-implementation/SKILL.md) |
| 프로토콜 코드젠 보기 | [skills/devian-tools/11-builder/40-codegen-protocol](../devian-tools/11-builder/40-codegen-protocol/SKILL.md) |
| Unity 정책 확인하기 | [skills/devian-unity/01-policy](../devian-unity/01-policy/SKILL.md) |
| 샘플 작성하기 | [skills/devian-unity/07-samples-creation-guide](../devian-unity/07-samples-creation-guide/SKILL.md) |
| Game 도메인 전체 보기 | [40-game-system/11-domain-game](../devian-unity/40-game-system/11-domain-game/SKILL.md) |

---

## Skill Groups

| Group | Overview | Policy | SSOT | 설명 |
|-------|----------|--------|------|------|
| **devian** | [00-overview](./00-overview/SKILL.md) | [01-policy](./01-policy/SKILL.md) | — | 공통 인덱스, 용어, workspace |
| **devian/10-module** | [00-overview](./10-module/00-overview/SKILL.md) | [01-policy](./10-module/01-policy/SKILL.md) | [03-ssot](./10-module/03-ssot/SKILL.md) | Root SSOT, 스킬 규격, 런타임 |
| **devian-tools** | [00-overview](../devian-tools/00-overview/SKILL.md) | [01-policy](../devian-tools/01-policy/SKILL.md) | [03-ssot](../devian-tools/03-ssot/SKILL.md) | 아카이브, CLI 도구 |
| **devian-tools/11-builder** | [00-overview](../devian-tools/11-builder/00-overview/SKILL.md) | [01-policy](../devian-tools/11-builder/01-policy/SKILL.md) | [03-ssot](../devian-tools/11-builder/03-ssot/SKILL.md) | 빌드, 테이블, 계약, NDJSON, PB64, 프로토콜 코드젠, 에러 리포팅 |
| **devian-unity/11-common-system** | [00-overview](../devian-unity/11-common-system/00-overview/SKILL.md) | [01-policy](../devian-unity/11-common-system/01-policy/SKILL.md) | — | Common 도메인, Feature 모듈 |
| **devian-unity** | [00-overview](../devian-unity/00-overview/SKILL.md) | [01-policy](../devian-unity/01-policy/SKILL.md) | [03-ssot](../devian-unity/03-ssot/SKILL.md) | Unity UPM, 컴포넌트 |
| **devian-examples** | [00-overview](../devian-examples/00-overview/SKILL.md) | [01-policy](../devian-examples/01-policy/SKILL.md) | [03-ssot](../devian-examples/03-ssot/SKILL.md) | 예제 도메인/프로토콜 |
| **devian-unity/40-game-system** | [00-overview](../devian-unity/40-game-system/00-overview/SKILL.md) | [01-policy](../devian-unity/40-game-system/01-policy/SKILL.md) | — | Game System 샘플 |
| **devian-unity/50-mobile-system** | [00-overview](../devian-unity/50-mobile-system/00-overview/SKILL.md) | [01-policy](../devian-unity/50-mobile-system/01-policy/SKILL.md) | — | MobileSystem 샘플 |

---

## Reference

- Root Index: [skills/SKILL.md](../SKILL.md)
- SSOT: [skills/devian/10-module/03-ssot](./10-module/03-ssot/SKILL.md)
